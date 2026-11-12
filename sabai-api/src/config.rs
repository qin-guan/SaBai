//! Configuration.
//!
//! Keys, sections and defaults are the ones the previous API used, so a deployment's
//! environment carries over unchanged: `Auth__SeedDemoUsers=true`,
//! `ConnectionStrings__sabai=Host=...`, `Cors__AllowedOrigins__0=...`, or as arguments
//! `--Auth:SeedDemoUsers=true`. Keys are case-insensitive. Later sources win:
//! defaults, then environment variables, then command-line arguments.

use std::collections::BTreeMap;
use std::path::PathBuf;
use std::time::Duration;

use anyhow::{Context, bail};

/// A flat, case-insensitive key/value view (`Section:Key`), like `IConfiguration`.
#[derive(Debug, Default, Clone)]
pub struct Config {
    values: BTreeMap<String, String>,
}

impl Config {
    pub fn from_process() -> Self {
        Self::from_sources(std::env::vars(), std::env::args().skip(1))
    }

    pub fn from_sources(
        env: impl IntoIterator<Item = (String, String)>,
        args: impl IntoIterator<Item = String>,
    ) -> Self {
        let mut config = Config::default();
        for (key, value) in env {
            config.set(&key.replace("__", ":"), value);
        }

        let args: Vec<String> = args.into_iter().collect();
        let mut index = 0;
        while index < args.len() {
            let arg = &args[index];
            index += 1;
            let Some(key) = arg.strip_prefix("--").or_else(|| arg.strip_prefix('/')) else {
                continue;
            };
            if let Some((key, value)) = key.split_once('=') {
                config.set(key, value.to_owned());
            } else if index < args.len() {
                config.set(key, args[index].clone());
                index += 1;
            }
        }
        config
    }

    pub fn set(&mut self, key: &str, value: impl Into<String>) {
        self.values.insert(key.to_ascii_lowercase(), value.into());
    }

    pub fn get(&self, key: &str) -> Option<&str> {
        self.values.get(&key.to_ascii_lowercase()).map(String::as_str)
    }

    fn text(&self, key: &str) -> Option<String> {
        self.get(key).map(str::to_owned)
    }

    fn non_blank(&self, key: &str) -> Option<String> {
        self.text(key).filter(|value| !value.trim().is_empty())
    }

    fn parsed<T: std::str::FromStr>(&self, key: &str) -> anyhow::Result<Option<T>> {
        match self.get(key) {
            None => Ok(None),
            Some(raw) => raw
                .trim()
                .parse()
                .map(Some)
                .map_err(|_| anyhow::anyhow!("Configuration value '{key}' is not valid: '{raw}'")),
        }
    }

    fn flag(&self, key: &str) -> anyhow::Result<Option<bool>> {
        match self.get(key).map(|raw| raw.trim().to_ascii_lowercase()) {
            None => Ok(None),
            Some(raw) => match raw.as_str() {
                "true" | "1" => Ok(Some(true)),
                "false" | "0" => Ok(Some(false)),
                _ => bail!("Configuration value '{key}' is not a boolean: '{raw}'"),
            },
        }
    }

    /// Values of an array section: `Section:0`, `Section:1`, …
    fn list(&self, key: &str) -> Option<Vec<String>> {
        let prefix = format!("{}:", key.to_ascii_lowercase());
        let mut items: Vec<(usize, String)> = self
            .values
            .iter()
            .filter_map(|(k, v)| Some((k.strip_prefix(&prefix)?.parse().ok()?, v.clone())))
            .collect();
        if items.is_empty() {
            return None;
        }
        items.sort_by_key(|(index, _)| *index);
        Some(items.into_iter().map(|(_, value)| value).collect())
    }
}

/// `Key=Value;Key=Value` connection strings (Npgsql, Aspire's Qdrant and RustFS ones).
pub fn parse_connection_string(raw: &str) -> BTreeMap<String, String> {
    raw.split(';')
        .filter_map(|part| part.split_once('='))
        .map(|(key, value)| (key.trim().to_ascii_lowercase(), value.trim().to_owned()))
        .collect()
}

#[derive(Debug, Clone)]
pub struct Settings {
    pub environment: String,
    pub listen: Vec<String>,
    pub database: DatabaseSettings,
    pub auth: AuthSettings,
    pub identity: IdentitySettings,
    pub cors_allowed_origins: Vec<String>,
    pub storage: StorageSettings,
    pub qdrant: QdrantSettings,
    pub face: FaceRecognitionSettings,
    pub otlp_endpoint: Option<String>,
}

impl Settings {
    pub fn is_development(&self) -> bool {
        self.environment.eq_ignore_ascii_case("Development")
    }

    pub fn is_production(&self) -> bool {
        self.environment.eq_ignore_ascii_case("Production")
    }

    pub fn load(config: &Config) -> anyhow::Result<Self> {
        let environment = config
            .non_blank("ASPNETCORE_ENVIRONMENT")
            .or_else(|| config.non_blank("DOTNET_ENVIRONMENT"))
            .or_else(|| config.non_blank("SABAI_ENVIRONMENT"))
            .unwrap_or_else(|| "Production".to_owned());
        let settings = Settings {
            listen: listen_addresses(config)?,
            database: DatabaseSettings::load(config)?,
            auth: AuthSettings::load(config)?,
            identity: IdentitySettings::load(config)?,
            cors_allowed_origins: config.list("Cors:AllowedOrigins").unwrap_or_else(|| {
                vec!["http://localhost:3000".to_owned(), "http://localhost:3300".to_owned()]
            }),
            storage: StorageSettings::load(config),
            qdrant: QdrantSettings::load(config),
            face: FaceRecognitionSettings::load(config)?,
            otlp_endpoint: config.non_blank("OTEL_EXPORTER_OTLP_ENDPOINT"),
            environment,
        };
        settings.face.validate()?;
        Ok(settings)
    }
}

/// `ASPNETCORE_URLS` (`http://+:8080;http://localhost:5000`), `ASPNETCORE_HTTP_PORTS`
/// (`8080;8081`) or `PORT`. Defaults to port 8080 on every interface.
fn listen_addresses(config: &Config) -> anyhow::Result<Vec<String>> {
    let mut addresses = Vec::new();
    if let Some(urls) = config.non_blank("ASPNETCORE_URLS").or_else(|| config.non_blank("urls")) {
        for url in urls.split(';').map(str::trim).filter(|url| !url.is_empty()) {
            let Some(rest) = url.strip_prefix("http://") else {
                // TLS is terminated in front of the API; https bindings are not served here.
                continue;
            };
            let rest = rest.trim_end_matches('/');
            let (host, port) = rest.rsplit_once(':').unwrap_or((rest, "80"));
            let host = match host {
                "+" | "*" | "0.0.0.0" | "[::]" => "0.0.0.0",
                "localhost" => "127.0.0.1",
                other => other,
            };
            addresses.push(format!("{host}:{port}"));
        }
    } else if let Some(ports) = config.non_blank("ASPNETCORE_HTTP_PORTS") {
        for port in ports.split(';').map(str::trim).filter(|port| !port.is_empty()) {
            addresses.push(format!("0.0.0.0:{port}"));
        }
    } else if let Some(port) = config.non_blank("PORT") {
        addresses.push(format!("0.0.0.0:{port}"));
    }
    if addresses.is_empty() {
        addresses.push("0.0.0.0:8080".to_owned());
    }
    Ok(addresses)
}

#[derive(Debug, Clone)]
pub struct DatabaseSettings {
    /// A `postgres://` URL, or an Npgsql connection string (`Host=...;Username=...`).
    pub connection: String,
    pub max_connections: u32,
}

impl DatabaseSettings {
    fn load(config: &Config) -> anyhow::Result<Self> {
        let connection = config
            .non_blank("ConnectionStrings:sabai")
            .or_else(|| config.non_blank("DATABASE_URL"))
            .context("Connection string 'sabai' is missing (set ConnectionStrings__sabai or DATABASE_URL).")?;
        Ok(DatabaseSettings {
            connection,
            max_connections: config.parsed("Database:MaxConnections")?.unwrap_or(100),
        })
    }

    pub fn connect_options(&self) -> anyhow::Result<sqlx::postgres::PgConnectOptions> {
        connect_options(&self.connection)
    }
}

pub fn connect_options(connection: &str) -> anyhow::Result<sqlx::postgres::PgConnectOptions> {
    use sqlx::postgres::{PgConnectOptions, PgSslMode};

    if connection.starts_with("postgres://") || connection.starts_with("postgresql://") {
        return connection.parse().context("The database URL is not valid.");
    }

    let parts = parse_connection_string(connection);
    let get = |names: &[&str]| names.iter().find_map(|name| parts.get(*name).cloned());
    let mut options = PgConnectOptions::new_without_pgpass()
        .host(&get(&["host", "server"]).unwrap_or_else(|| "localhost".to_owned()));
    if let Some(port) = get(&["port"]) {
        options = options.port(port.parse().context("The database port is not a number.")?);
    }
    if let Some(user) = get(&["username", "user id", "user", "uid"]) {
        options = options.username(&user);
    }
    if let Some(password) = get(&["password", "pwd"]) {
        options = options.password(&password);
    }
    if let Some(database) = get(&["database", "db"]) {
        options = options.database(&database);
    }
    if let Some(mode) = get(&["ssl mode", "sslmode"]) {
        options = options.ssl_mode(match mode.to_ascii_lowercase().as_str() {
            "disable" => PgSslMode::Disable,
            "allow" => PgSslMode::Allow,
            "prefer" => PgSslMode::Prefer,
            "require" => PgSslMode::Require,
            "verifyca" | "verify-ca" => PgSslMode::VerifyCa,
            "verifyfull" | "verify-full" => PgSslMode::VerifyFull,
            other => bail!("Unknown SSL mode '{other}' in the database connection string."),
        });
    }
    Ok(options)
}

#[derive(Debug, Clone)]
pub struct AuthSettings {
    /// How long a sign-in cookie stays valid without activity (sliding).
    pub session_lifetime: Duration,
    /// How often a signed-in cookie is re-checked against the database, so role
    /// changes, locks and password resets reach existing sessions.
    pub security_stamp_validation: Duration,
    /// Issuer shown in authenticator apps next to the account name.
    pub authenticator_issuer: String,
    /// Seed the demo staff accounts (development and tests only).
    pub seed_demo_users: bool,
    /// Optional first admin, created at startup if missing, or given this password if
    /// the account has none yet.
    pub bootstrap_admin: BootstrapAdmin,
}

#[derive(Debug, Clone, Default)]
pub struct BootstrapAdmin {
    pub user_name: String,
    pub password: String,
    pub email: Option<String>,
}

impl AuthSettings {
    fn load(config: &Config) -> anyhow::Result<Self> {
        let minutes = |key: &str, default: u64| -> anyhow::Result<Duration> {
            Ok(Duration::from_secs(60 * config.parsed::<u64>(key)?.unwrap_or(default)))
        };
        Ok(AuthSettings {
            session_lifetime: minutes("Auth:SessionLifetimeMinutes", 480)?,
            security_stamp_validation: minutes("Auth:SecurityStampValidationMinutes", 1)?,
            authenticator_issuer: config
                .text("Auth:AuthenticatorIssuer")
                .unwrap_or_else(|| "Sa'Bai".to_owned()),
            seed_demo_users: config.flag("Auth:SeedDemoUsers")?.unwrap_or(false),
            bootstrap_admin: BootstrapAdmin {
                user_name: config.text("Auth:BootstrapAdmin:UserName").unwrap_or_default(),
                password: config.text("Auth:BootstrapAdmin:Password").unwrap_or_default(),
                email: config.non_blank("Auth:BootstrapAdmin:Email"),
            },
        })
    }
}

/// Password, lockout and username rules. Same defaults and `Identity:*` keys as before.
#[derive(Debug, Clone)]
pub struct IdentitySettings {
    pub password_required_length: usize,
    pub password_required_unique_chars: usize,
    pub password_require_digit: bool,
    pub password_require_lowercase: bool,
    pub password_require_uppercase: bool,
    pub password_require_non_alphanumeric: bool,
    pub lockout_allowed_for_new_users: bool,
    pub lockout_max_failed_access_attempts: i32,
    pub lockout_default_time_span: Duration,
    pub allowed_user_name_characters: String,
}

impl Default for IdentitySettings {
    fn default() -> Self {
        IdentitySettings {
            password_required_length: 8,
            password_required_unique_chars: 1,
            password_require_digit: false,
            password_require_lowercase: false,
            password_require_uppercase: false,
            password_require_non_alphanumeric: false,
            lockout_allowed_for_new_users: true,
            lockout_max_failed_access_attempts: 5,
            lockout_default_time_span: Duration::from_secs(5 * 60),
            allowed_user_name_characters:
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+".to_owned(),
        }
    }
}

impl IdentitySettings {
    fn load(config: &Config) -> anyhow::Result<Self> {
        let d = IdentitySettings::default();
        Ok(IdentitySettings {
            password_required_length: config
                .parsed("Identity:Password:RequiredLength")?
                .unwrap_or(d.password_required_length),
            password_required_unique_chars: config
                .parsed("Identity:Password:RequiredUniqueChars")?
                .unwrap_or(d.password_required_unique_chars),
            password_require_digit: config
                .flag("Identity:Password:RequireDigit")?
                .unwrap_or(d.password_require_digit),
            password_require_lowercase: config
                .flag("Identity:Password:RequireLowercase")?
                .unwrap_or(d.password_require_lowercase),
            password_require_uppercase: config
                .flag("Identity:Password:RequireUppercase")?
                .unwrap_or(d.password_require_uppercase),
            password_require_non_alphanumeric: config
                .flag("Identity:Password:RequireNonAlphanumeric")?
                .unwrap_or(d.password_require_non_alphanumeric),
            lockout_allowed_for_new_users: config
                .flag("Identity:Lockout:AllowedForNewUsers")?
                .unwrap_or(d.lockout_allowed_for_new_users),
            lockout_max_failed_access_attempts: config
                .parsed("Identity:Lockout:MaxFailedAccessAttempts")?
                .unwrap_or(d.lockout_max_failed_access_attempts),
            lockout_default_time_span: match config.get("Identity:Lockout:DefaultLockoutTimeSpan") {
                Some(raw) => parse_time_span(raw)?,
                None => d.lockout_default_time_span,
            },
            allowed_user_name_characters: config
                .text("Identity:User:AllowedUserNameCharacters")
                .unwrap_or(d.allowed_user_name_characters),
        })
    }
}

/// `[d.]hh:mm[:ss]`, the .NET `TimeSpan` text form.
fn parse_time_span(raw: &str) -> anyhow::Result<Duration> {
    let invalid = || anyhow::anyhow!("'{raw}' is not a valid time span (expected hh:mm:ss).");
    let (days, clock) = match raw.trim().split_once('.') {
        Some((days, clock)) if clock.contains(':') => (days.parse::<u64>().map_err(|_| invalid())?, clock),
        _ => (0, raw.trim()),
    };
    let parts: Vec<u64> = clock
        .split(':')
        .map(|part| part.split('.').next().unwrap_or("").parse::<u64>())
        .collect::<Result<_, _>>()
        .map_err(|_| invalid())?;
    let seconds = match parts.as_slice() {
        [h, m] => h * 3600 + m * 60,
        [h, m, s] => h * 3600 + m * 60 + s,
        _ => return Err(invalid()),
    };
    Ok(Duration::from_secs(days * 86_400 + seconds))
}

/// S3-compatible settings for the RustFS bucket.
#[derive(Debug, Clone)]
pub struct StorageSettings {
    pub bucket: String,
    pub region: String,
    pub endpoint: String,
    pub access_key: String,
    pub secret_key: String,
}

impl StorageSettings {
    fn load(config: &Config) -> Self {
        let mut settings = StorageSettings {
            bucket: config.non_blank("Storage:Bucket").unwrap_or_else(|| "sabai".to_owned()),
            region: config.non_blank("Storage:Region").unwrap_or_else(|| "us-east-1".to_owned()),
            endpoint: config.text("Storage:Endpoint").unwrap_or_default(),
            access_key: config.text("Storage:AccessKey").unwrap_or_default(),
            secret_key: config.text("Storage:SecretKey").unwrap_or_default(),
        };
        let connection = config
            .non_blank("ConnectionStrings:fs-sabai")
            .or_else(|| config.non_blank("ConnectionStrings:fs"));
        if let Some(connection) = connection {
            let parsed = parse_connection_string(&connection);
            let apply = |key: &str, target: &mut String| {
                if let Some(value) = parsed.get(key) {
                    *target = value.clone();
                }
            };
            apply("endpoint", &mut settings.endpoint);
            apply("accesskey", &mut settings.access_key);
            apply("secretkey", &mut settings.secret_key);
            apply("bucket", &mut settings.bucket);
        }
        settings
    }

    pub fn is_configured(&self) -> bool {
        !self.endpoint.trim().is_empty()
    }
}

/// Qdrant gRPC endpoint, from the `qdrant` connection string (`Endpoint=http://host:6334;Key=...`).
#[derive(Debug, Clone, Default)]
pub struct QdrantSettings {
    pub endpoint: Option<String>,
    pub api_key: Option<String>,
}

impl QdrantSettings {
    fn load(config: &Config) -> Self {
        let Some(connection) = config.non_blank("ConnectionStrings:qdrant") else {
            return QdrantSettings::default();
        };
        if !connection.contains('=') {
            return QdrantSettings { endpoint: Some(connection), api_key: None };
        }
        let parsed = parse_connection_string(&connection);
        QdrantSettings {
            endpoint: parsed.get("endpoint").cloned(),
            api_key: parsed.get("key").cloned().filter(|key| !key.is_empty()),
        }
    }
}

#[derive(Debug, Clone)]
pub struct FaceRecognitionSettings {
    /// Directory holding the ONNX models. Defaults to a per-user data directory.
    pub models_directory: PathBuf,
    /// File name of the SCRFD face detection model inside `models_directory`.
    pub detector_model: String,
    /// File name of the ArcFace recognition model inside `models_directory`.
    pub recognizer_model: String,
    /// Download missing models from `model_pack_url` on first use.
    pub auto_download_models: bool,
    /// Zip archive containing both models (InsightFace buffalo_l pack by default).
    pub model_pack_url: String,
    /// Square input size the detector runs at.
    pub detection_size: u32,
    /// Minimum detector confidence for a face to be kept.
    pub detection_threshold: f32,
    /// Retry missed or small faces at higher resolution, with shadow lifting and roll correction.
    pub enable_detection_fallback: bool,
    /// Square detector size for best-effort retries.
    pub detection_fallback_size: u32,
    /// Detector confidence for retries only. This does not lower the identity-match threshold.
    pub detection_fallback_threshold: f32,
    /// IoU threshold for non-maximum suppression.
    pub nms_threshold: f32,
    /// Minimum face width and height in source pixels for recognition; zero disables the size guard.
    pub minimum_face_size: u32,
    /// Minimum distance between the eyes in source pixels; zero disables the resolution guard.
    pub minimum_eye_distance: f32,
    /// Cosine similarity at or above which two faces are considered the same person.
    pub match_threshold: f32,
    /// Qdrant collection storing face embeddings.
    pub collection_name: String,
    /// Use the CUDA execution provider when available.
    pub use_cuda: bool,
}

impl Default for FaceRecognitionSettings {
    fn default() -> Self {
        let data_home = std::env::var_os("XDG_DATA_HOME")
            .map(PathBuf::from)
            .filter(|path| path.is_absolute())
            .or_else(|| std::env::var_os("HOME").map(|home| PathBuf::from(home).join(".local/share")))
            .unwrap_or_else(|| PathBuf::from("."));
        FaceRecognitionSettings {
            models_directory: data_home.join("sabai").join("models"),
            detector_model: "det_10g.onnx".to_owned(),
            recognizer_model: "w600k_r50.onnx".to_owned(),
            auto_download_models: true,
            model_pack_url:
                "https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_l.zip"
                    .to_owned(),
            detection_size: 640,
            detection_threshold: 0.5,
            enable_detection_fallback: true,
            detection_fallback_size: 1024,
            detection_fallback_threshold: 0.35,
            nms_threshold: 0.4,
            minimum_face_size: 0,
            minimum_eye_distance: 0.0,
            match_threshold: 0.4,
            collection_name: "faces".to_owned(),
            use_cuda: false,
        }
    }
}

impl FaceRecognitionSettings {
    fn load(config: &Config) -> anyhow::Result<Self> {
        let d = FaceRecognitionSettings::default();
        let key = |name: &str| format!("FaceRecognition:{name}");
        Ok(FaceRecognitionSettings {
            models_directory: config
                .non_blank(&key("ModelsDirectory"))
                .map(PathBuf::from)
                .unwrap_or(d.models_directory),
            detector_model: config.non_blank(&key("DetectorModel")).unwrap_or(d.detector_model),
            recognizer_model: config.non_blank(&key("RecognizerModel")).unwrap_or(d.recognizer_model),
            auto_download_models: config
                .flag(&key("AutoDownloadModels"))?
                .unwrap_or(d.auto_download_models),
            model_pack_url: config.non_blank(&key("ModelPackUrl")).unwrap_or(d.model_pack_url),
            detection_size: config.parsed(&key("DetectionSize"))?.unwrap_or(d.detection_size),
            detection_threshold: config
                .parsed(&key("DetectionThreshold"))?
                .unwrap_or(d.detection_threshold),
            enable_detection_fallback: config
                .flag(&key("EnableDetectionFallback"))?
                .unwrap_or(d.enable_detection_fallback),
            detection_fallback_size: config
                .parsed(&key("DetectionFallbackSize"))?
                .unwrap_or(d.detection_fallback_size),
            detection_fallback_threshold: config
                .parsed(&key("DetectionFallbackThreshold"))?
                .unwrap_or(d.detection_fallback_threshold),
            nms_threshold: config.parsed(&key("NmsThreshold"))?.unwrap_or(d.nms_threshold),
            minimum_face_size: config.parsed(&key("MinimumFaceSize"))?.unwrap_or(d.minimum_face_size),
            minimum_eye_distance: config
                .parsed(&key("MinimumEyeDistance"))?
                .unwrap_or(d.minimum_eye_distance),
            match_threshold: config.parsed(&key("MatchThreshold"))?.unwrap_or(d.match_threshold),
            collection_name: config.non_blank(&key("CollectionName")).unwrap_or(d.collection_name),
            use_cuda: config.flag(&key("UseCuda"))?.unwrap_or(d.use_cuda),
        })
    }

    pub fn validate(&self) -> anyhow::Result<()> {
        if self.detection_size == 0 || self.detection_size % 32 != 0 {
            bail!("DetectionSize must be a positive multiple of 32.");
        }
        if !(0.0..=1.0).contains(&self.detection_threshold) {
            bail!("DetectionThreshold must be between 0 and 1.");
        }
        if self.detection_fallback_size == 0 || self.detection_fallback_size % 32 != 0 {
            bail!("DetectionFallbackSize must be a positive multiple of 32.");
        }
        if !(self.detection_fallback_threshold >= 0.0
            && self.detection_fallback_threshold <= self.detection_threshold)
        {
            bail!("DetectionFallbackThreshold must be between zero and DetectionThreshold.");
        }
        if !(0.0..=1.0).contains(&self.nms_threshold) {
            bail!("NmsThreshold must be between 0 and 1.");
        }
        if !(self.minimum_eye_distance.is_finite() && self.minimum_eye_distance >= 0.0) {
            bail!("MinimumEyeDistance must be finite and non-negative.");
        }
        if !(-1.0..=1.0).contains(&self.match_threshold) {
            bail!("MatchThreshold must be between -1 and 1.");
        }
        Ok(())
    }

    pub fn detector_path(&self) -> PathBuf {
        self.models_directory.join(&self.detector_model)
    }

    pub fn recognizer_path(&self) -> PathBuf {
        self.models_directory.join(&self.recognizer_model)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn config(env: &[(&str, &str)], args: &[&str]) -> Config {
        Config::from_sources(
            env.iter().map(|(k, v)| (k.to_string(), v.to_string())),
            args.iter().map(|a| a.to_string()),
        )
    }

    #[test]
    fn arguments_override_environment_and_keys_ignore_case() {
        let config = config(
            &[("Auth__SeedDemoUsers", "false"), ("ConnectionStrings__sabai", "Host=db;Database=sabai")],
            &["--auth:seeddemousers=true", "--Auth:SecurityStampValidationMinutes", "0"],
        );
        let settings = Settings::load(&config).unwrap();
        assert!(settings.auth.seed_demo_users);
        assert_eq!(settings.auth.security_stamp_validation, Duration::ZERO);
        assert_eq!(settings.auth.session_lifetime, Duration::from_secs(480 * 60));
    }

    #[test]
    fn reads_aspire_connection_strings_and_lists() {
        let config = config(
            &[
                ("ConnectionStrings__sabai", "Host=db;Port=5433;Username=u;Password=p;Database=sabai"),
                ("ConnectionStrings__fs-sabai", "Endpoint=http://fs:9000;AccessKey=a;SecretKey=s;Bucket=b"),
                ("ConnectionStrings__qdrant", "Endpoint=http://qdrant:6334;Key=secret"),
                ("Cors__AllowedOrigins__1", "https://b"),
                ("Cors__AllowedOrigins__0", "https://a"),
                ("ASPNETCORE_URLS", "http://+:9999"),
                ("Identity__Lockout__DefaultLockoutTimeSpan", "00:10:00"),
            ],
            &[],
        );
        let settings = Settings::load(&config).unwrap();
        assert_eq!(settings.storage.endpoint, "http://fs:9000");
        assert_eq!(settings.storage.bucket, "b");
        assert_eq!(settings.qdrant.endpoint.as_deref(), Some("http://qdrant:6334"));
        assert_eq!(settings.qdrant.api_key.as_deref(), Some("secret"));
        assert_eq!(settings.cors_allowed_origins, ["https://a", "https://b"]);
        assert_eq!(settings.listen, ["0.0.0.0:9999"]);
        assert_eq!(settings.identity.lockout_default_time_span, Duration::from_secs(600));
        settings.database.connect_options().unwrap();
    }
}
