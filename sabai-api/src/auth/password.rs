//! Password hashing in the ASP.NET Core Identity V3 format, so accounts created by the
//! .NET API keep their passwords:
//!
//! `0x01 | prf (u32 BE) | iterations (u32 BE) | salt length (u32 BE) | salt | subkey`, base64.
//!
//! New hashes use PBKDF2-HMAC-SHA512, 100 000 iterations, a 16-byte salt and a 32-byte key
//! (the Identity defaults). Hashes with other V3 parameters still verify.

use base64::Engine;
use base64::engine::general_purpose::STANDARD;
use hmac::Hmac;
use rand::RngCore;
use sha1::Sha1;
use sha2::{Sha256, Sha512};
use subtle::ConstantTimeEq;

const FORMAT_V3: u8 = 0x01;
const PRF_HMAC_SHA1: u32 = 0;
const PRF_HMAC_SHA256: u32 = 1;
const PRF_HMAC_SHA512: u32 = 2;
const ITERATIONS: u32 = 100_000;
const SALT_LENGTH: usize = 16;
const KEY_LENGTH: usize = 32;

pub fn hash(password: &str) -> String {
    let mut salt = [0u8; SALT_LENGTH];
    rand::rng().fill_bytes(&mut salt);
    let mut key = [0u8; KEY_LENGTH];
    derive(PRF_HMAC_SHA512, password, &salt, ITERATIONS, &mut key);

    let mut out = Vec::with_capacity(13 + SALT_LENGTH + KEY_LENGTH);
    out.push(FORMAT_V3);
    out.extend_from_slice(&PRF_HMAC_SHA512.to_be_bytes());
    out.extend_from_slice(&ITERATIONS.to_be_bytes());
    out.extend_from_slice(&(SALT_LENGTH as u32).to_be_bytes());
    out.extend_from_slice(&salt);
    out.extend_from_slice(&key);
    STANDARD.encode(out)
}

pub fn verify(hash: &str, password: &str) -> bool {
    let Ok(bytes) = STANDARD.decode(hash) else {
        return false;
    };
    if bytes.len() < 13 || bytes[0] != FORMAT_V3 {
        return false;
    }
    let word = |at: usize| u32::from_be_bytes([bytes[at], bytes[at + 1], bytes[at + 2], bytes[at + 3]]);
    let (prf, iterations, salt_length) = (word(1), word(5), word(9) as usize);
    if salt_length < 16 || iterations == 0 || bytes.len() < 13 + salt_length + 16 {
        return false;
    }
    let salt = &bytes[13..13 + salt_length];
    let expected = &bytes[13 + salt_length..];
    let mut actual = vec![0u8; expected.len()];
    if !derive(prf, password, salt, iterations, &mut actual) {
        return false;
    }
    actual.ct_eq(expected).into()
}

fn derive(prf: u32, password: &str, salt: &[u8], iterations: u32, out: &mut [u8]) -> bool {
    let password = password.as_bytes();
    let result = match prf {
        PRF_HMAC_SHA1 => pbkdf2::pbkdf2::<Hmac<Sha1>>(password, salt, iterations, out),
        PRF_HMAC_SHA256 => pbkdf2::pbkdf2::<Hmac<Sha256>>(password, salt, iterations, out),
        PRF_HMAC_SHA512 => pbkdf2::pbkdf2::<Hmac<Sha512>>(password, salt, iterations, out),
        _ => return false,
    };
    result.is_ok()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips() {
        let hashed = hash("Password1!");
        assert!(verify(&hashed, "Password1!"));
        assert!(!verify(&hashed, "password1!"));
        assert!(!verify("not base64", "x"));
        assert!(!verify("", "x"));
    }

    /// A hash written by ASP.NET Core Identity 10 for "Password1!".
    #[test]
    fn verifies_hashes_written_by_dotnet() {
        let dotnet = include_str!("testdata/dotnet_password_hash.txt").trim();
        assert!(verify(dotnet, "Password1!"));
        assert!(!verify(dotnet, "Password2!"));
    }
}
