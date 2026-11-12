use std::sync::LazyLock;

use regex::Regex;

use crate::error::ApiError;

pub const ACUITY_MESSAGE: &str = "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120) or CF, HM, LP, NLP";

pub const PINHOLE_MESSAGE: &str =
    "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120), CF, HM, LP, NLP, or NI";

static ACUITY: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"(?i)^(6/(7\.5|6|9|12|15|18|24|36|60|120)([+-]\d{1,2})?|CF|HM|LP|NLP)$").unwrap()
});

static PINHOLE: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"(?i)^(6/(7\.5|6|9|12|15|18|24|36|60|120)([+-]\d{1,2})?|CF|HM|LP|NLP|NI)$").unwrap()
});

/// Trims and upper-cases a visual acuity; blank is no value. Anything else is a 400.
pub fn normalize_acuity(value: Option<&str>) -> Result<Option<String>, ApiError> {
    normalize(value, &ACUITY, ACUITY_MESSAGE)
}

pub fn normalize_pinhole(value: Option<&str>) -> Result<Option<String>, ApiError> {
    normalize(value, &PINHOLE, PINHOLE_MESSAGE)
}

fn normalize(value: Option<&str>, pattern: &Regex, message: &str) -> Result<Option<String>, ApiError> {
    let Some(trimmed) = value.map(str::trim).filter(|value| !value.is_empty()) else {
        return Ok(None);
    };
    if !pattern.is_match(trimmed) {
        return Err(ApiError::bad_request(message));
    }
    Ok(Some(trimmed.to_uppercase()))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn normalizes_and_rejects() {
        assert_eq!(normalize_acuity(Some(" 6/9+2 ")).unwrap().as_deref(), Some("6/9+2"));
        assert_eq!(normalize_acuity(Some("cf")).unwrap().as_deref(), Some("CF"));
        assert_eq!(normalize_acuity(Some("  ")).unwrap(), None);
        assert_eq!(normalize_acuity(None).unwrap(), None);
        assert!(normalize_acuity(Some("NI")).is_err());
        assert_eq!(normalize_pinhole(Some("ni")).unwrap().as_deref(), Some("NI"));
        assert!(normalize_pinhole(Some("6/7")).is_err());
    }
}
