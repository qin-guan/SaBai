//! Authenticator (TOTP, RFC 6238) codes and the secrets behind them, compatible with
//! what ASP.NET Core Identity stored: Base32 keys of 20 random bytes, HMAC-SHA1,
//! 30-second steps, six digits, accepted up to two steps either side.

use data_encoding::BASE32_NOPAD;
use hmac::{Hmac, Mac};
use rand::RngCore;
use sha1::Sha1;

const STEP_SECONDS: i64 = 30;
const ACCEPTED_DRIFT_STEPS: i64 = 2;

/// 20 random bytes as 32 Base32 characters. Also the shape of a security stamp.
pub fn new_base32_secret() -> String {
    let mut bytes = [0u8; 20];
    rand::rng().fill_bytes(&mut bytes);
    BASE32_NOPAD.encode(&bytes)
}

pub fn code_at(key: &[u8], step: i64) -> u32 {
    let mut mac = Hmac::<Sha1>::new_from_slice(key).expect("HMAC accepts any key length");
    mac.update(&(step as u64).to_be_bytes());
    let digest = mac.finalize().into_bytes();
    let offset = (digest[19] & 0x0f) as usize;
    let binary = u32::from_be_bytes([
        digest[offset] & 0x7f,
        digest[offset + 1],
        digest[offset + 2],
        digest[offset + 3],
    ]);
    binary % 1_000_000
}

fn decode_key(key: &str) -> Option<Vec<u8>> {
    BASE32_NOPAD.decode(key.trim().trim_end_matches('=').to_ascii_uppercase().as_bytes()).ok()
}

/// The six-digit code for `key` at `unix_seconds`.
pub fn generate(key: &str, unix_seconds: i64) -> Option<String> {
    let key = decode_key(key)?;
    Some(format!("{:06}", code_at(&key, unix_seconds.div_euclid(STEP_SECONDS))))
}

pub fn verify(key: &str, code: &str, unix_seconds: i64) -> bool {
    let Ok(code) = code.trim().parse::<u32>() else {
        return false;
    };
    let Some(key) = decode_key(key) else {
        return false;
    };
    let step = unix_seconds.div_euclid(STEP_SECONDS);
    (-ACCEPTED_DRIFT_STEPS..=ACCEPTED_DRIFT_STEPS).any(|drift| code_at(&key, step + drift) == code)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn matches_rfc_6238_vectors() {
        // RFC 6238 appendix B, SHA-1, key "12345678901234567890", truncated to six digits.
        let key = b"12345678901234567890";
        assert_eq!(code_at(key, 59 / 30), 287_082);
        assert_eq!(code_at(key, 1_111_111_109 / 30), 81_804);
        assert_eq!(code_at(key, 2_000_000_000 / 30), 279_037);
    }

    #[test]
    fn accepts_nearby_steps_only() {
        let key = new_base32_secret();
        assert_eq!(key.len(), 32);
        let now = 1_800_000_000;
        let code = generate(&key, now).unwrap();
        assert!(verify(&key, &code, now));
        assert!(verify(&key, &code, now + 60));
        assert!(verify(&key, &code, now - 60));
        assert!(!verify(&key, &code, now + 200));
        assert!(!verify(&key, "abc", now));
    }
}
