//! Request validation rules with the messages the previous API returned.
//!
//! Implement [`Validate`] for a request type and add rules in `validate`; every failing
//! rule is reported, like before. Rules other than `not_empty`/`not_null` pass when the
//! value is absent, so optional fields are only checked when given.
//!
//! ```ignore
//! impl Validate for Request {
//!     fn validate(&self, v: &mut Rules) {
//!         v.not_empty("code", &self.code).max_len("code", &self.code, 255);
//!         v.greater_than("villageCodeId", self.village_code_id, 0);
//!     }
//! }
//! ```
//!
//! Property names are the camelCase JSON names; messages show them with the words split
//! (`'user Name' must not be empty.`), as clients have always seen them.

use std::fmt::Display;

use regex::Regex;

use crate::error::{ApiError, FieldError};

pub trait Validate {
    fn validate(&self, _rules: &mut Rules) {}
}

impl Validate for () {}

#[derive(Debug, Default)]
pub struct Rules {
    errors: Vec<FieldError>,
}

/// Text that may be absent: `String`, `&str`, `Option<String>`, `Option<&str>`.
pub trait Text {
    fn text(&self) -> Option<&str>;
}

impl Text for String {
    fn text(&self) -> Option<&str> {
        Some(self)
    }
}

impl Text for &str {
    fn text(&self) -> Option<&str> {
        Some(self)
    }
}

impl Text for Option<String> {
    fn text(&self) -> Option<&str> {
        self.as_deref()
    }
}

impl Text for Option<&str> {
    fn text(&self) -> Option<&str> {
        *self
    }
}

impl<T: Text> Text for &T {
    fn text(&self) -> Option<&str> {
        (*self).text()
    }
}

/// `userName` → `user Name`, `hasBS2Card` → `has BS 2 Card`: the display name used in messages.
pub fn display_name(property: &str) -> String {
    let property = property.rsplit('.').next().unwrap_or(property);
    let chars: Vec<char> = property.chars().collect();
    let mut out = String::with_capacity(property.len() + 4);
    for (i, &c) in chars.iter().enumerate() {
        if i > 0 {
            let prev = chars[i - 1];
            let next_is_lower = chars.get(i + 1).is_some_and(|n| n.is_ascii_lowercase());
            let split = (prev.is_ascii_uppercase() && c.is_ascii_uppercase() && next_is_lower)
                || (!prev.is_ascii_uppercase() && c.is_ascii_uppercase())
                || (prev.is_ascii_alphabetic() && !c.is_ascii_alphabetic());
            if split {
                out.push(' ');
            }
        }
        out.push(c);
    }
    out
}

impl Rules {
    pub fn new() -> Self {
        Rules::default()
    }

    pub fn is_valid(&self) -> bool {
        self.errors.is_empty()
    }

    pub fn into_result(self) -> Result<(), ApiError> {
        if self.errors.is_empty() {
            return Ok(());
        }
        // A lone failure is repeated as the problem's detail.
        let detail = (self.errors.len() == 1).then(|| self.errors[0].reason.clone());
        Err(ApiError::Validation { detail, errors: self.errors })
    }

    /// Adds a failure with a custom message.
    pub fn add(&mut self, name: &str, reason: impl Into<String>) -> &mut Self {
        self.errors.push(FieldError::new(name, reason));
        self
    }

    /// A custom rule: fails with `reason` unless `ok`.
    pub fn must(&mut self, name: &str, ok: bool, reason: impl Into<String>) -> &mut Self {
        if !ok {
            self.add(name, reason);
        }
        self
    }

    /// Present and not blank.
    pub fn not_empty(&mut self, name: &str, value: impl Text) -> &mut Self {
        let ok = value.text().is_some_and(|text| !text.trim().is_empty());
        self.must(name, ok, format!("'{}' must not be empty.", display_name(name)))
    }

    /// A value was given (for optional numbers, dates, enums, lists).
    pub fn not_null<T>(&mut self, name: &str, value: &Option<T>) -> &mut Self {
        self.must(name, value.is_some(), format!("'{}' must not be empty.", display_name(name)))
    }

    /// A non-empty list.
    pub fn not_empty_list<T>(&mut self, name: &str, value: &[T]) -> &mut Self {
        self.must(name, !value.is_empty(), format!("'{}' must not be empty.", display_name(name)))
    }

    pub fn max_len(&mut self, name: &str, value: impl Text, max: usize) -> &mut Self {
        if let Some(text) = value.text() {
            let length = text.encode_utf16().count();
            if length > max {
                self.add(
                    name,
                    format!(
                        "The length of '{}' must be {max} characters or fewer. You entered {length} characters.",
                        display_name(name)
                    ),
                );
            }
        }
        self
    }

    pub fn min_len(&mut self, name: &str, value: impl Text, min: usize) -> &mut Self {
        if let Some(text) = value.text() {
            let length = text.encode_utf16().count();
            if length < min {
                self.add(
                    name,
                    format!(
                        "The length of '{}' must be at least {min} characters. You entered {length} characters.",
                        display_name(name)
                    ),
                );
            }
        }
        self
    }

    pub fn matches(&mut self, name: &str, value: impl Text, pattern: &Regex) -> &mut Self {
        if let Some(text) = value.text() {
            if !pattern.is_match(text) {
                self.add(name, format!("'{}' is not in the correct format.", display_name(name)));
            }
        }
        self
    }

    /// The loose check used before: one `@`, not first or last.
    pub fn email(&mut self, name: &str, value: impl Text) -> &mut Self {
        if let Some(text) = value.text() {
            let at = text.find('@');
            let ok = at.is_some_and(|at| at > 0 && at < text.len() - 1 && at == text.rfind('@').unwrap());
            if !ok {
                self.add(name, format!("'{}' is not a valid email address.", display_name(name)));
            }
        }
        self
    }

    pub fn greater_than<T: PartialOrd + Display>(
        &mut self,
        name: &str,
        value: impl Into<Option<T>>,
        than: T,
    ) -> &mut Self {
        if let Some(value) = value.into() {
            if !(value > than) {
                self.add(name, format!("'{}' must be greater than '{than}'.", display_name(name)));
            }
        }
        self
    }

    pub fn greater_than_or_equal<T: PartialOrd + Display>(
        &mut self,
        name: &str,
        value: impl Into<Option<T>>,
        than: T,
    ) -> &mut Self {
        if let Some(value) = value.into() {
            if !(value >= than) {
                self.add(
                    name,
                    format!("'{}' must be greater than or equal to '{than}'.", display_name(name)),
                );
            }
        }
        self
    }

    pub fn less_than<T: PartialOrd + Display>(
        &mut self,
        name: &str,
        value: impl Into<Option<T>>,
        than: T,
    ) -> &mut Self {
        if let Some(value) = value.into() {
            if !(value < than) {
                self.add(name, format!("'{}' must be less than '{than}'.", display_name(name)));
            }
        }
        self
    }

    pub fn less_than_or_equal<T: PartialOrd + Display>(
        &mut self,
        name: &str,
        value: impl Into<Option<T>>,
        than: T,
    ) -> &mut Self {
        if let Some(value) = value.into() {
            if !(value <= than) {
                self.add(
                    name,
                    format!("'{}' must be less than or equal to '{than}'.", display_name(name)),
                );
            }
        }
        self
    }

    pub fn between<T: PartialOrd + Display>(
        &mut self,
        name: &str,
        value: impl Into<Option<T>>,
        from: T,
        to: T,
    ) -> &mut Self {
        if let Some(value) = value.into() {
            if !(value >= from && value <= to) {
                self.add(
                    name,
                    format!(
                        "'{}' must be between {from} and {to}. You entered {value}.",
                        display_name(name)
                    ),
                );
            }
        }
        self
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn display_names_split_words_like_before() {
        assert_eq!(display_name("userName"), "user Name");
        assert_eq!(display_name("password"), "password");
        assert_eq!(display_name("hasBS2Card"), "has BS 2 Card");
        assert_eq!(display_name("villageCodeId"), "village Code Id");
        assert_eq!(display_name("vitals.heartRate"), "heart Rate");
    }

    #[test]
    fn messages_match() {
        let mut rules = Rules::new();
        rules
            .not_empty("userName", "  ")
            .max_len("code", "abcd", 3)
            .greater_than("id", 0, 0)
            .between("take", Some(0), 1, 200)
            .email("email", "nope")
            .not_empty("nickname", None::<String>)
            .max_len("nickname", None::<String>, 1)
            .greater_than("take", None::<i32>, 0);
        let reasons: Vec<_> = rules.errors.iter().map(|e| e.reason.as_str()).collect();
        assert_eq!(
            reasons,
            [
                "'user Name' must not be empty.",
                "The length of 'code' must be 3 characters or fewer. You entered 4 characters.",
                "'id' must be greater than '0'.",
                "'take' must be between 1 and 200. You entered 0.",
                "'email' is not a valid email address.",
                "'nickname' must not be empty.",
            ]
        );
    }
}
