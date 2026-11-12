//! Timestamps as the API speaks them.
//!
//! `Ts` is a UTC instant with microsecond precision (what Postgres `timestamptz` keeps).
//! It serializes the way the previous API did (`2026-11-12T11:06:17.568126+00:00`, trailing
//! zeros of the fraction trimmed) and parses any ISO 8601 date or date-time; a value
//! without an offset is taken as UTC.

use std::fmt;

use chrono::{DateTime, Duration, NaiveDate, NaiveDateTime, SubsecRound, TimeZone, Utc};
use serde::{Deserialize, Deserializer, Serialize, Serializer};
use sqlx::encode::IsNull;
use sqlx::error::BoxDynError;
use sqlx::postgres::{PgArgumentBuffer, PgHasArrayType, PgTypeInfo, PgValueFormat, PgValueRef};
use sqlx::{Decode, Encode, Postgres, Type};

#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Ts(pub DateTime<Utc>);

impl Ts {
    /// The current time, cut to the microsecond so it equals what the database stores.
    pub fn now() -> Self {
        Ts(Utc::now().trunc_subsecs(6))
    }

    /// "Never": the far-future instant used for an admin lock. Stored as `infinity`.
    pub fn max() -> Self {
        Ts(Utc.with_ymd_and_hms(9999, 12, 31, 23, 59, 59).unwrap() + Duration::microseconds(999_999))
    }

    pub fn is_max(&self) -> bool {
        *self >= Self::max()
    }

    pub fn min() -> Self {
        Ts(Utc.with_ymd_and_hms(1, 1, 1, 0, 0, 0).unwrap())
    }

    pub fn from_unix_seconds(seconds: i64) -> Self {
        Ts(Utc.timestamp_opt(seconds, 0).single().unwrap_or_default())
    }

    pub fn unix_seconds(&self) -> i64 {
        self.0.timestamp()
    }

    pub fn date(&self) -> NaiveDate {
        self.0.date_naive()
    }

    /// Parses an ISO 8601 date or date-time. No offset means UTC.
    pub fn parse(text: &str) -> Option<Self> {
        let text = text.trim();
        if let Ok(value) = DateTime::parse_from_rfc3339(text) {
            return Some(Ts(value.with_timezone(&Utc).trunc_subsecs(6)));
        }
        for format in ["%Y-%m-%dT%H:%M:%S%.f", "%Y-%m-%dT%H:%M", "%Y-%m-%d %H:%M:%S%.f"] {
            if let Ok(value) = NaiveDateTime::parse_from_str(text, format) {
                return Some(Ts(Utc.from_utc_datetime(&value).trunc_subsecs(6)));
            }
        }
        for format in ["%Y-%m-%dT%H:%M:%S%.f%:z", "%Y-%m-%dT%H:%M%:z", "%Y-%m-%dT%H:%M:%S%.f%z"] {
            if let Ok(value) = DateTime::parse_from_str(text, format) {
                return Some(Ts(value.with_timezone(&Utc).trunc_subsecs(6)));
            }
        }
        NaiveDate::parse_from_str(text, "%Y-%m-%d")
            .ok()
            .and_then(|date| date.and_hms_opt(0, 0, 0))
            .map(|value| Ts(Utc.from_utc_datetime(&value)))
    }

    /// JSON form: fraction only when non-zero, trailing zeros trimmed, `+00:00` offset.
    pub fn to_json_string(&self) -> String {
        if self.is_max() {
            return "9999-12-31T23:59:59.9999999+00:00".to_owned();
        }
        let base = self.0.format("%Y-%m-%dT%H:%M:%S");
        let micros = self.0.timestamp_subsec_micros();
        if micros == 0 {
            format!("{base}+00:00")
        } else {
            let fraction = format!("{micros:06}");
            format!("{base}.{}+00:00", fraction.trim_end_matches('0'))
        }
    }

    /// Round-trip ("O") form used in audit entries: always seven fraction digits.
    pub fn to_audit_string(&self) -> String {
        if self.is_max() {
            return "9999-12-31T23:59:59.9999999+00:00".to_owned();
        }
        format!(
            "{}.{:06}0+00:00",
            self.0.format("%Y-%m-%dT%H:%M:%S"),
            self.0.timestamp_subsec_micros()
        )
    }
}

impl fmt::Debug for Ts {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.to_json_string())
    }
}

impl fmt::Display for Ts {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.to_json_string())
    }
}

impl std::ops::Add<Duration> for Ts {
    type Output = Ts;
    fn add(self, rhs: Duration) -> Ts {
        Ts(self.0 + rhs)
    }
}

impl std::ops::Sub<Duration> for Ts {
    type Output = Ts;
    fn sub(self, rhs: Duration) -> Ts {
        Ts(self.0 - rhs)
    }
}

impl std::ops::Sub<Ts> for Ts {
    type Output = Duration;
    fn sub(self, rhs: Ts) -> Duration {
        self.0 - rhs.0
    }
}

impl Serialize for Ts {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        serializer.serialize_str(&self.to_json_string())
    }
}

impl<'de> Deserialize<'de> for Ts {
    fn deserialize<D: Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
        let text = String::deserialize(deserializer)?;
        Ts::parse(&text).ok_or_else(|| {
            serde::de::Error::custom(format!("Value [{text}] is not valid for a [DateTimeOffset] property!"))
        })
    }
}

// Postgres stores timestamptz as microseconds since 2000-01-01; i64::MAX is `infinity`.

fn pg_epoch() -> DateTime<Utc> {
    Utc.with_ymd_and_hms(2000, 1, 1, 0, 0, 0).unwrap()
}

impl Type<Postgres> for Ts {
    fn type_info() -> PgTypeInfo {
        <DateTime<Utc> as Type<Postgres>>::type_info()
    }

    fn compatible(ty: &PgTypeInfo) -> bool {
        <DateTime<Utc> as Type<Postgres>>::compatible(ty)
    }
}

impl PgHasArrayType for Ts {
    fn array_type_info() -> PgTypeInfo {
        <DateTime<Utc> as PgHasArrayType>::array_type_info()
    }
}

impl Encode<'_, Postgres> for Ts {
    fn encode_by_ref(&self, buf: &mut PgArgumentBuffer) -> Result<IsNull, BoxDynError> {
        let micros = if self.is_max() {
            i64::MAX
        } else {
            (self.0 - pg_epoch()).num_microseconds().ok_or("timestamp out of range")?
        };
        buf.extend_from_slice(&micros.to_be_bytes());
        Ok(IsNull::No)
    }
}

impl<'r> Decode<'r, Postgres> for Ts {
    fn decode(value: PgValueRef<'r>) -> Result<Self, BoxDynError> {
        match value.format() {
            PgValueFormat::Binary => {
                let bytes: [u8; 8] = value.as_bytes()?.try_into()?;
                Ok(match i64::from_be_bytes(bytes) {
                    i64::MAX => Ts::max(),
                    i64::MIN => Ts::min(),
                    micros => Ts(pg_epoch() + Duration::microseconds(micros)),
                })
            }
            PgValueFormat::Text => match value.as_str()? {
                "infinity" => Ok(Ts::max()),
                "-infinity" => Ok(Ts::min()),
                text => DateTime::parse_from_str(text, "%Y-%m-%d %H:%M:%S%.f%#z")
                    .map(|value| Ts(value.with_timezone(&Utc)))
                    .map_err(Into::into),
            },
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn json_trims_fraction_and_uses_numeric_offset() {
        let ts = Ts::parse("2026-11-12T11:06:17.568100Z").unwrap();
        assert_eq!(ts.to_json_string(), "2026-11-12T11:06:17.5681+00:00");
        assert_eq!(ts.to_audit_string(), "2026-11-12T11:06:17.5681000+00:00");
        assert_eq!(Ts::parse("1990-01-01").unwrap().to_json_string(), "1990-01-01T00:00:00+00:00");
    }

    #[test]
    fn parses_offsets_and_bare_date_times() {
        assert_eq!(
            Ts::parse("2026-01-01T07:00:00+07:00").unwrap(),
            Ts::parse("2026-01-01T00:00:00").unwrap()
        );
        assert!(Ts::parse("x").is_none());
    }

    #[test]
    fn max_is_the_dotnet_max_value() {
        assert_eq!(Ts::max().to_json_string(), "9999-12-31T23:59:59.9999999+00:00");
        assert!(Ts::max().is_max());
        assert!(!Ts::now().is_max());
    }
}
