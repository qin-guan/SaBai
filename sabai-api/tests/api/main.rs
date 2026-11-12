//! Integration tests: the real application over HTTP against a real Postgres database.
//!
//! One server is started for the whole run (see `support`), on a database created for
//! it. Tests share that server and run in parallel, so each test creates the records it
//! needs (with unique names) and never assumes it is alone.
//!
//! Needs a Postgres server; set `SABAI_TEST_POSTGRES` to its admin URL (default
//! `postgres://postgres@localhost:5432/postgres`).

mod support;

mod audit;
mod auth;
mod broadcasts;
mod consults;
mod contract;
mod faces;
mod files;
mod patients;
mod pharmacy;
mod records;
mod stress;
mod village_codes;
mod visits;
