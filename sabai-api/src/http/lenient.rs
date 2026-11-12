//! A forgiving deserializer over a JSON value, matching how the previous API read
//! requests so existing clients keep working:
//!
//! - property names match in any case (`UserName`, `username`, `userName`);
//! - numbers and booleans may arrive as strings (`"170.5"`, `"true"`), which is also how
//!   route, query and form values arrive;
//! - a `null` for a text field reads as empty text, leaving it to validation to reject;
//! - a single value is accepted where a list is expected (`?ids=1`);
//! - unknown properties are ignored.
//!
//! Errors carry the path of the offending property, reported as the error's `name`.

use std::fmt;

use serde::de::{self, DeserializeOwned, DeserializeSeed, IntoDeserializer, MapAccess, SeqAccess, Visitor};
use serde_json::Value;

/// Name used for errors that concern the body as a whole, not one property.
pub const SERIALIZER_ERRORS: &str = "serializerErrors";

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BindError {
    /// Path segments from the root, outermost first; empty for whole-body errors.
    path: Vec<String>,
    pub message: String,
}

impl BindError {
    fn new(message: impl Into<String>) -> Self {
        BindError { path: Vec::new(), message: message.into() }
    }

    fn under(mut self, segment: String) -> Self {
        self.path.insert(0, segment);
        self
    }

    /// `vitals.temperature`, `roles[0]`, or `serializerErrors`.
    pub fn name(&self) -> String {
        if self.path.is_empty() {
            return SERIALIZER_ERRORS.to_owned();
        }
        let mut name = String::new();
        for segment in &self.path {
            if !name.is_empty() && !segment.starts_with('[') {
                name.push('.');
            }
            name.push_str(segment);
        }
        name
    }
}

impl fmt::Display for BindError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for BindError {}

impl de::Error for BindError {
    fn custom<T: fmt::Display>(message: T) -> Self {
        BindError::new(message.to_string())
    }

    fn missing_field(field: &'static str) -> Self {
        BindError::new(format!(
            "JSON deserialization was missing required properties including: '{field}'."
        ))
    }
}

pub fn from_value<T: DeserializeOwned>(value: &Value) -> Result<T, BindError> {
    T::deserialize(De(value))
}

struct De<'a>(&'a Value);

fn token(value: &Value) -> &'static str {
    match value {
        Value::Null => "Null",
        Value::Bool(true) => "True",
        Value::Bool(false) => "False",
        Value::Number(_) => "Number",
        Value::String(_) => "String",
        Value::Array(_) => "StartArray",
        Value::Object(_) => "StartObject",
    }
}

/// The .NET-style name of the type a visitor produces, for error messages.
fn type_label<T>() -> String {
    let full = std::any::type_name::<T>();
    let inner = full
        .strip_prefix("core::option::Option<")
        .and_then(|rest| rest.strip_suffix('>'))
        .unwrap_or(full);
    let short = inner.rsplit("::").next().unwrap_or(inner);
    match short {
        "i8" => "SByte",
        "i16" => "Int16",
        "i32" => "Int32",
        "i64" => "Int64",
        "u8" => "Byte",
        "u16" => "UInt16",
        "u32" => "UInt32",
        "u64" => "UInt64",
        "f32" => "Single",
        "f64" => "Double",
        "bool" => "Boolean",
        "Uuid" => "Guid",
        "Ts" => "DateTimeOffset",
        "NaiveDate" => "DateOnly",
        "String" | "str" => "String",
        other => other,
    }
    .to_owned()
}

fn not_valid<T>(raw: impl fmt::Display) -> BindError {
    BindError::new(format!("Value [{raw}] is not valid for a [{}] property!", type_label::<T>()))
}

fn not_convertible<T>() -> BindError {
    BindError::new(format!("The JSON value could not be converted to {}.", type_label::<T>()))
}

fn raw_text(value: &Value) -> String {
    match value {
        Value::String(text) => text.clone(),
        other => other.to_string(),
    }
}

macro_rules! integer {
    ($method:ident, $visit:ident, $ty:ty) => {
        fn $method<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
            let parsed: Option<$ty> = match self.0 {
                Value::Number(number) => number
                    .as_i64()
                    .and_then(|n| <$ty>::try_from(n).ok())
                    .or_else(|| number.as_u64().and_then(|n| <$ty>::try_from(n).ok()))
                    .or_else(|| {
                        number.as_f64().filter(|f| f.fract() == 0.0).and_then(|f| {
                            let n = f as i128;
                            <$ty>::try_from(n).ok()
                        })
                    }),
                Value::String(text) => text.trim().parse().ok(),
                Value::Array(_) => return Err(not_valid::<$ty>(join_values(self.0))),
                _ => return Err(not_convertible::<$ty>()),
            };
            match parsed {
                Some(value) => visitor.$visit(value),
                None => Err(not_valid::<$ty>(raw_text(self.0))),
            }
        }
    };
}

macro_rules! float {
    ($method:ident, $visit:ident, $ty:ty) => {
        fn $method<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
            let parsed: Option<$ty> = match self.0 {
                Value::Number(number) => number.as_f64().map(|f| f as $ty),
                Value::String(text) => text.trim().parse().ok().filter(|f: &$ty| f.is_finite()),
                Value::Array(_) => return Err(not_valid::<$ty>(join_values(self.0))),
                _ => return Err(not_convertible::<$ty>()),
            };
            match parsed {
                Some(value) => visitor.$visit(value),
                None => Err(not_valid::<$ty>(raw_text(self.0))),
            }
        }
    };
}

/// A repeated query key (`?take=2&take=1`) offered to a single-value property.
fn join_values(value: &Value) -> String {
    match value {
        Value::Array(items) => items.iter().map(raw_text).collect::<Vec<_>>().join(","),
        other => raw_text(other),
    }
}

impl<'de> de::Deserializer<'de> for De<'de> {
    type Error = BindError;

    fn deserialize_any<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::Null => visitor.visit_unit().map_err(|_: BindError| not_convertible::<V::Value>()),
            Value::Bool(value) => visitor.visit_bool(*value),
            Value::Number(number) => {
                if let Some(value) = number.as_i64() {
                    visitor.visit_i64(value)
                } else if let Some(value) = number.as_u64() {
                    visitor.visit_u64(value)
                } else {
                    visitor.visit_f64(number.as_f64().unwrap_or(f64::NAN))
                }
            }
            Value::String(text) => visitor
                .visit_str(text.trim())
                .map_err(|error: BindError| relabel::<V::Value>(error, text)),
            Value::Array(_) => self.deserialize_seq(visitor),
            Value::Object(_) => self.deserialize_map(visitor),
        }
    }

    fn deserialize_bool<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::Bool(value) => visitor.visit_bool(*value),
            Value::String(text) if text.trim().eq_ignore_ascii_case("true") => visitor.visit_bool(true),
            Value::String(text) if text.trim().eq_ignore_ascii_case("false") => visitor.visit_bool(false),
            Value::String(text) => Err(not_valid::<bool>(text)),
            Value::Array(_) => Err(not_valid::<bool>(join_values(self.0))),
            other => Err(BindError::new(format!(
                "Cannot get the value of a token type '{}' as a boolean.",
                token(other)
            ))),
        }
    }

    integer!(deserialize_i8, visit_i8, i8);
    integer!(deserialize_i16, visit_i16, i16);
    integer!(deserialize_i32, visit_i32, i32);
    integer!(deserialize_i64, visit_i64, i64);
    integer!(deserialize_u8, visit_u8, u8);
    integer!(deserialize_u16, visit_u16, u16);
    integer!(deserialize_u32, visit_u32, u32);
    integer!(deserialize_u64, visit_u64, u64);
    float!(deserialize_f32, visit_f32, f32);
    float!(deserialize_f64, visit_f64, f64);

    fn deserialize_char<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_str(visitor)
    }

    fn deserialize_str<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::String(text) => visitor
                .visit_str(text)
                .map_err(|error: BindError| relabel::<V::Value>(error, text)),
            // Text sent as null reads as empty; validation decides whether that is allowed.
            Value::Null => visitor.visit_str("").map_err(|_: BindError| not_convertible::<V::Value>()),
            Value::Array(_) => Err(not_valid::<V::Value>(join_values(self.0))),
            other => Err(BindError::new(format!(
                "Cannot get the value of a token type '{}' as a string.",
                token(other)
            ))),
        }
    }

    fn deserialize_string<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_str(visitor)
    }

    fn deserialize_bytes<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_str(visitor)
    }

    fn deserialize_byte_buf<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_str(visitor)
    }

    fn deserialize_option<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::Null => visitor.visit_none(),
            _ => visitor.visit_some(self),
        }
    }

    fn deserialize_unit<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        visitor.visit_unit()
    }

    fn deserialize_unit_struct<V: Visitor<'de>>(
        self,
        _name: &'static str,
        visitor: V,
    ) -> Result<V::Value, BindError> {
        visitor.visit_unit()
    }

    fn deserialize_newtype_struct<V: Visitor<'de>>(
        self,
        _name: &'static str,
        visitor: V,
    ) -> Result<V::Value, BindError> {
        visitor.visit_newtype_struct(self)
    }

    fn deserialize_seq<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::Array(items) => visitor.visit_seq(Seq { items: items.iter(), index: 0 }),
            Value::Null => Err(not_convertible::<V::Value>()),
            // One value where a list is expected, e.g. a query key given once.
            single => visitor.visit_seq(Seq { items: std::slice::from_ref(single).iter(), index: 0 }),
        }
    }

    fn deserialize_tuple<V: Visitor<'de>>(self, _len: usize, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_seq(visitor)
    }

    fn deserialize_tuple_struct<V: Visitor<'de>>(
        self,
        _name: &'static str,
        _len: usize,
        visitor: V,
    ) -> Result<V::Value, BindError> {
        self.deserialize_seq(visitor)
    }

    fn deserialize_map<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        match self.0 {
            Value::Object(map) => {
                let entries = map.iter().map(|(key, value)| (key.as_str(), value)).collect();
                visitor.visit_map(Map { entries, next: 0 })
            }
            _ => Err(not_convertible::<V::Value>()),
        }
    }

    fn deserialize_struct<V: Visitor<'de>>(
        self,
        _name: &'static str,
        fields: &'static [&'static str],
        visitor: V,
    ) -> Result<V::Value, BindError> {
        let Value::Object(map) = self.0 else {
            return Err(not_convertible::<V::Value>());
        };
        // Match property names in any case; when a name repeats, the last one wins.
        let mut entries: Vec<(&str, &Value)> = Vec::with_capacity(map.len());
        for (key, value) in map {
            let name = fields
                .iter()
                .copied()
                .find(|field| field.eq_ignore_ascii_case(key))
                .unwrap_or(key.as_str());
            match entries.iter_mut().find(|(existing, _)| *existing == name) {
                Some(entry) => entry.1 = value,
                None => entries.push((name, value)),
            }
        }
        visitor.visit_map(Map { entries, next: 0 })
    }

    fn deserialize_enum<V: Visitor<'de>>(
        self,
        _name: &'static str,
        variants: &'static [&'static str],
        visitor: V,
    ) -> Result<V::Value, BindError> {
        match self.0 {
            Value::String(text) => {
                let text = text.trim();
                let variant = variants
                    .iter()
                    .copied()
                    .find(|variant| variant.eq_ignore_ascii_case(text))
                    .ok_or_else(|| not_valid::<V::Value>(text))?;
                visitor.visit_enum(variant.into_deserializer())
            }
            Value::Number(number) => {
                let variant = number
                    .as_u64()
                    .and_then(|index| variants.get(index as usize).copied())
                    .ok_or_else(|| not_valid::<V::Value>(number))?;
                visitor.visit_enum(variant.into_deserializer())
            }
            _ => Err(not_convertible::<V::Value>()),
        }
    }

    fn deserialize_identifier<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        self.deserialize_str(visitor)
    }

    fn deserialize_ignored_any<V: Visitor<'de>>(self, visitor: V) -> Result<V::Value, BindError> {
        visitor.visit_unit()
    }
}

/// Rewords a leaf type's own parse error (a UUID, a date, an enum) in the API's style,
/// keeping messages that already are.
fn relabel<T>(error: BindError, raw: &str) -> BindError {
    if !error.path.is_empty() || error.message.starts_with("Value [") {
        error
    } else {
        not_valid::<T>(raw)
    }
}

struct Seq<'a> {
    items: std::slice::Iter<'a, Value>,
    index: usize,
}

impl<'de> SeqAccess<'de> for Seq<'de> {
    type Error = BindError;

    fn next_element_seed<T: DeserializeSeed<'de>>(&mut self, seed: T) -> Result<Option<T::Value>, BindError> {
        let Some(item) = self.items.next() else {
            return Ok(None);
        };
        let index = self.index;
        self.index += 1;
        seed.deserialize(De(item)).map(Some).map_err(|error| error.under(format!("[{index}]")))
    }

    fn size_hint(&self) -> Option<usize> {
        Some(self.items.len())
    }
}

struct Map<'a> {
    entries: Vec<(&'a str, &'a Value)>,
    next: usize,
}

impl<'de> MapAccess<'de> for Map<'de> {
    type Error = BindError;

    fn next_key_seed<K: DeserializeSeed<'de>>(&mut self, seed: K) -> Result<Option<K::Value>, BindError> {
        match self.entries.get(self.next) {
            Some((key, _)) => seed.deserialize((*key).into_deserializer()).map(Some),
            None => Ok(None),
        }
    }

    fn next_value_seed<V: DeserializeSeed<'de>>(&mut self, seed: V) -> Result<V::Value, BindError> {
        let (key, value) = self.entries[self.next];
        self.next += 1;
        seed.deserialize(De(value)).map_err(|error| error.under(key.to_owned()))
    }
}

#[cfg(test)]
mod tests {
    use rust_decimal::Decimal;
    use serde::Deserialize;
    use serde_json::json;
    use uuid::Uuid;

    use super::*;
    use crate::time::Ts;

    #[derive(Debug, Deserialize)]
    #[serde(rename_all = "camelCase")]
    struct Inner {
        temperature: Option<Decimal>,
    }

    #[derive(Debug, Deserialize)]
    #[serde(rename_all = "camelCase")]
    struct Sample {
        user_name: String,
        #[serde(default)]
        village_code_id: i32,
        #[serde(default = "yes")]
        is_visible: bool,
        id: Option<Uuid>,
        date_of_birth: Option<Ts>,
        vitals: Option<Inner>,
        #[serde(default)]
        roles: Vec<String>,
        nickname: Option<String>,
    }

    fn yes() -> bool {
        true
    }

    fn read(value: Value) -> Result<Sample, BindError> {
        from_value(&value)
    }

    #[test]
    fn names_match_in_any_case_and_numbers_may_be_strings() {
        let sample = read(json!({
            "USERNAME": "a", "VillageCodeId": "7", "isVisible": "false", "extra": 1,
            "vitals": {"Temperature": "36.50"}, "roles": "admin", "dateOfBirth": "1990-01-01",
        }))
        .unwrap();
        assert_eq!(sample.user_name, "a");
        assert_eq!(sample.village_code_id, 7);
        assert!(!sample.is_visible);
        assert_eq!(sample.vitals.unwrap().temperature, Some("36.5".parse().unwrap()));
        assert_eq!(sample.roles, ["admin"]);
        assert!(sample.date_of_birth.is_some() && sample.id.is_none() && sample.nickname.is_none());
    }

    #[test]
    fn decimals_read_from_json_numbers() {
        let sample = read(json!({"userName": "a", "vitals": {"temperature": 36.6}})).unwrap();
        assert_eq!(sample.vitals.unwrap().temperature.unwrap().to_string(), "36.6");
    }

    #[test]
    fn null_text_reads_as_empty_and_defaults_apply() {
        let sample = read(json!({"userName": null})).unwrap();
        assert_eq!(sample.user_name, "");
        assert_eq!(sample.village_code_id, 0);
        assert!(sample.is_visible);
    }

    #[test]
    fn errors_name_the_property() {
        let missing = read(json!({})).unwrap_err();
        assert_eq!(missing.name(), "serializerErrors");
        assert!(missing.message.contains("'userName'"));

        let wrong = read(json!({"userName": 5})).unwrap_err();
        assert_eq!(wrong.name(), "userName");
        assert_eq!(wrong.message, "Cannot get the value of a token type 'Number' as a string.");

        let number = read(json!({"userName": "a", "villageCodeId": "abc"})).unwrap_err();
        assert_eq!((number.name().as_str(), number.message.as_str()),
            ("villageCodeId", "Value [abc] is not valid for a [Int32] property!"));

        let guid = read(json!({"userName": "a", "id": "not-a-guid"})).unwrap_err();
        assert_eq!(guid.message, "Value [not-a-guid] is not valid for a [Guid] property!");

        let nested = read(json!({"userName": "a", "vitals": {"temperature": "abc"}})).unwrap_err();
        assert_eq!(nested.name(), "vitals.temperature");

        let flag = read(json!({"userName": "a", "isVisible": null})).unwrap_err();
        assert_eq!(flag.message, "Cannot get the value of a token type 'Null' as a boolean.");
    }
}
