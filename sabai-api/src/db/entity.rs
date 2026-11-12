//! Maps a struct to a table and describes its columns, so that inserts, updates and
//! deletes can be written once (`Tx::insert` / `update` / `delete`) and audited the same
//! way for every table. Declare entities with the [`entity!`] macro.

use std::any::Any;

use chrono::NaiveDate;
use rust_decimal::Decimal;
use sqlx::postgres::PgRow;
use sqlx::{Encode, Postgres, QueryBuilder, Type};
use uuid::Uuid;

use crate::time::Ts;

/// What a column is, for SQL generation and for the audit log.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Kind {
    /// An ordinary column.
    Plain,
    /// Primary key the application supplies (a UUID, or part of a composite key).
    Pk,
    /// Primary key the database generates (identity column).
    PkAuto,
    /// Primary key column that is also a foreign key (join rows). Unlike other key
    /// columns it is listed among an audit entry's changes, to show the link.
    PkFk,
    /// Logged as `[redacted]`: the change is worth knowing, the value is a secret.
    Secret,
    /// Written to the database but never audited (changes on every write, says nothing).
    Unaudited,
}

impl Kind {
    pub fn is_key(self) -> bool {
        matches!(self, Kind::Pk | Kind::PkAuto | Kind::PkFk)
    }
}

#[derive(Debug, Clone, Copy)]
pub struct Field {
    /// Name shown in the audit log (the property name of the previous API).
    pub name: &'static str,
    /// Column name in the table.
    pub column: &'static str,
    pub kind: Kind,
}

/// How a value is written in an audit entry. `None` is SQL NULL.
pub trait AuditFormat {
    fn audit(&self) -> Option<String>;
}

macro_rules! audit_display {
    ($($ty:ty),*) => {$(
        impl AuditFormat for $ty {
            fn audit(&self) -> Option<String> {
                Some(self.to_string())
            }
        }
    )*};
}

audit_display!(i16, i32, i64, String, Uuid, Decimal);

impl AuditFormat for bool {
    fn audit(&self) -> Option<String> {
        Some(if *self { "true" } else { "false" }.to_owned())
    }
}

impl AuditFormat for Ts {
    fn audit(&self) -> Option<String> {
        Some(self.to_audit_string())
    }
}

impl AuditFormat for NaiveDate {
    fn audit(&self) -> Option<String> {
        Some(self.format("%Y-%m-%d").to_string())
    }
}

impl AuditFormat for Vec<String> {
    fn audit(&self) -> Option<String> {
        Some(serde_json::to_string(self).unwrap_or_default())
    }
}

impl<T: AuditFormat> AuditFormat for Option<T> {
    fn audit(&self) -> Option<String> {
        self.as_ref().and_then(AuditFormat::audit)
    }
}

/// A column value seen without its type: enough to bind it, compare it and log it.
pub trait Value: Send + Sync {
    fn audit(&self) -> Option<String>;
    fn same(&self, other: &dyn Value) -> bool;
    fn bind(&self, query: &mut QueryBuilder<'_, Postgres>);
    fn as_any(&self) -> &dyn Any;
}

impl<T> Value for T
where
    T: AuditFormat + PartialEq + Clone + Send + Sync + 'static,
    T: for<'q> Encode<'q, Postgres> + Type<Postgres>,
{
    fn audit(&self) -> Option<String> {
        AuditFormat::audit(self)
    }

    fn same(&self, other: &dyn Value) -> bool {
        other.as_any().downcast_ref::<T>().is_some_and(|other| other == self)
    }

    fn bind(&self, query: &mut QueryBuilder<'_, Postgres>) {
        query.push_bind(self.clone());
    }

    fn as_any(&self) -> &dyn Any {
        self
    }
}

pub trait Entity: Clone + Send + Sync + Unpin + 'static + for<'r> sqlx::FromRow<'r, PgRow> {
    /// Table name, unquoted.
    const TABLE: &'static str;
    /// Entity type name written to the audit log.
    const NAME: &'static str;
    const FIELDS: &'static [Field];

    /// Column values, in the order of [`Entity::FIELDS`].
    fn values(&self) -> Vec<&dyn Value>;

    /// Called just before the row is inserted.
    fn before_insert(&mut self, _now: Ts) {}

    /// `SELECT * FROM "Table"`, to be extended with `WHERE …`.
    fn select() -> String {
        format!("SELECT * FROM \"{}\"", Self::TABLE)
    }
}

/// Declares a table-backed struct.
///
/// ```ignore
/// entity! {
///     /// A village.
///     pub struct VillageCode in "VillageCodes" {
///         id: i32 = "Id" [PkAuto],
///         code: String = "Code",
///         // Column named differently from the audited property name:
///         photo: Option<String> = "PatientImagePublicId" as "PhotoStoredFileName",
///     }
/// }
/// ```
///
/// Options after the table name: `as "AuditName"` when the audit log's entity name
/// differs from the struct name, and `on_insert = path::to::fn` for a
/// `fn(&mut Self, Ts)` hook run before inserting.
#[macro_export]
macro_rules! entity {
    (
        $(#[$meta:meta])*
        pub struct $name:ident in $table:literal $(as $audit:literal)? $(, on_insert = $hook:path)? {
            $(
                $(#[$field_meta:meta])*
                $field:ident : $ty:ty = $column:literal $(as $prop:literal)? $([$kind:ident])?
            ),* $(,)?
        }
    ) => {
        $(#[$meta])*
        #[derive(Debug, Clone, sqlx::FromRow)]
        pub struct $name {
            $(
                $(#[$field_meta])*
                #[sqlx(rename = $column)]
                pub $field: $ty,
            )*
        }

        impl $crate::db::Entity for $name {
            const TABLE: &'static str = $table;
            const NAME: &'static str = $crate::entity!(@or stringify!($name) $(, $audit)?);
            const FIELDS: &'static [$crate::db::Field] = &[
                $($crate::db::Field {
                    name: $crate::entity!(@or $column $(, $prop)?),
                    column: $column,
                    kind: $crate::entity!(@kind $($kind)?),
                },)*
            ];

            fn values(&self) -> Vec<&dyn $crate::db::Value> {
                vec![$(&self.$field as &dyn $crate::db::Value,)*]
            }

            #[allow(unused_variables)]
            fn before_insert(&mut self, now: $crate::time::Ts) {
                $($hook(self, now);)?
            }
        }
    };
    (@or $default:expr) => { $default };
    (@or $default:expr, $given:expr) => { $given };
    (@kind) => { $crate::db::Kind::Plain };
    (@kind $kind:ident) => { $crate::db::Kind::$kind };
}

/// Declares an enum that is stored as text under its variant name and travels in JSON
/// under a wire name.
///
/// ```ignore
/// wire_enum! {
///     pub enum Gender { Male = "male", Female = "female" }
/// }
/// ```
///
/// Reading is forgiving, as before: the wire name or the variant name, in any case, or
/// the variant's index.
#[macro_export]
macro_rules! wire_enum {
    (
        $(#[$meta:meta])*
        pub enum $name:ident { $( $(#[$variant_meta:meta])* $variant:ident = $wire:literal ),* $(,)? }
    ) => {
        $(#[$meta])*
        #[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
        pub enum $name {
            $( $(#[$variant_meta])* $variant, )*
        }

        impl $name {
            pub const ALL: &'static [$name] = &[$($name::$variant,)*];

            /// The stored name (the variant name).
            pub fn name(self) -> &'static str {
                match self { $($name::$variant => stringify!($variant),)* }
            }

            /// The name used in JSON.
            pub fn wire(self) -> &'static str {
                match self { $($name::$variant => $wire,)* }
            }

            pub fn parse(text: &str) -> Option<Self> {
                let text = text.trim();
                Self::ALL.iter().copied().find(|value| {
                    value.wire().eq_ignore_ascii_case(text) || value.name().eq_ignore_ascii_case(text)
                })
            }

            pub fn from_index(index: u64) -> Option<Self> {
                Self::ALL.get(usize::try_from(index).ok()?).copied()
            }
        }

        impl std::fmt::Display for $name {
            fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.write_str(self.wire())
            }
        }

        impl serde::Serialize for $name {
            fn serialize<S: serde::Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
                serializer.serialize_str(self.wire())
            }
        }

        impl<'de> serde::Deserialize<'de> for $name {
            fn deserialize<D: serde::Deserializer<'de>>(deserializer: D) -> Result<Self, D::Error> {
                struct Visitor;
                impl serde::de::Visitor<'_> for Visitor {
                    type Value = $name;

                    fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                        write!(f, "a {} value", stringify!($name))
                    }

                    fn visit_str<E: serde::de::Error>(self, text: &str) -> Result<$name, E> {
                        $name::parse(text).ok_or_else(|| {
                            E::custom(format!(
                                "Value [{text}] is not valid for a [{}] property!",
                                stringify!($name)
                            ))
                        })
                    }

                    fn visit_u64<E: serde::de::Error>(self, index: u64) -> Result<$name, E> {
                        $name::from_index(index).ok_or_else(|| {
                            E::custom(format!(
                                "Value [{index}] is not valid for a [{}] property!",
                                stringify!($name)
                            ))
                        })
                    }

                    fn visit_i64<E: serde::de::Error>(self, index: i64) -> Result<$name, E> {
                        self.visit_u64(u64::try_from(index).unwrap_or(u64::MAX))
                    }
                }
                deserializer.deserialize_any(Visitor)
            }
        }

        impl sqlx::Type<sqlx::Postgres> for $name {
            fn type_info() -> sqlx::postgres::PgTypeInfo {
                <String as sqlx::Type<sqlx::Postgres>>::type_info()
            }

            fn compatible(ty: &sqlx::postgres::PgTypeInfo) -> bool {
                <String as sqlx::Type<sqlx::Postgres>>::compatible(ty)
            }
        }

        impl sqlx::postgres::PgHasArrayType for $name {
            fn array_type_info() -> sqlx::postgres::PgTypeInfo {
                <String as sqlx::postgres::PgHasArrayType>::array_type_info()
            }
        }

        impl sqlx::Encode<'_, sqlx::Postgres> for $name {
            fn encode_by_ref(
                &self,
                buf: &mut sqlx::postgres::PgArgumentBuffer,
            ) -> Result<sqlx::encode::IsNull, sqlx::error::BoxDynError> {
                <&str as sqlx::Encode<sqlx::Postgres>>::encode_by_ref(&self.name(), buf)
            }
        }

        impl<'r> sqlx::Decode<'r, sqlx::Postgres> for $name {
            fn decode(value: sqlx::postgres::PgValueRef<'r>) -> Result<Self, sqlx::error::BoxDynError> {
                let text = <&str as sqlx::Decode<sqlx::Postgres>>::decode(value)?;
                Self::ALL
                    .iter()
                    .copied()
                    .find(|candidate| candidate.name() == text)
                    .ok_or_else(|| format!("unknown {} value '{text}'", stringify!($name)).into())
            }
        }

        impl $crate::db::AuditFormat for $name {
            fn audit(&self) -> Option<String> {
                Some(self.name().to_owned())
            }
        }
    };
}
