//! The API description and the router must agree.

use std::collections::BTreeSet;

use reqwest::{Method, StatusCode};
use serde_json::Value;

use crate::support::*;

/// Every operation in `openapi/v1.json` is routed: calling it without a session is
/// answered by the endpoint (401, or a result for public ones), never 404 or 405.
#[tokio::test]
async fn every_documented_operation_has_a_route() {
    let document: Value = serde_json::from_str(sabai_api::openapi::DOCUMENT).unwrap();
    let client = Client::anonymous();
    let mut missing = BTreeSet::new();
    for (path, operations) in document["paths"].as_object().unwrap() {
        // Fill route parameters with a value that is valid for ids of either kind.
        let mut concrete = String::new();
        for segment in path.split('/').skip(1) {
            concrete.push('/');
            concrete.push_str(if segment.starts_with('{') { "00000000-0000-0000-0000-000000000000" } else { segment });
        }
        for method in operations.as_object().unwrap().keys() {
            let Ok(http_method) = Method::from_bytes(method.to_uppercase().as_bytes()) else {
                continue;
            };
            let response = client.request(http_method, &concrete).send().await.unwrap();
            if matches!(response.status(), StatusCode::NOT_FOUND | StatusCode::METHOD_NOT_ALLOWED) {
                missing.insert(format!("{} {path}", method.to_uppercase()));
            }
        }
    }
    assert!(missing.is_empty(), "documented but not routed:\n{}", missing.into_iter().collect::<Vec<_>>().join("\n"));
}

#[tokio::test]
async fn description_and_probes_are_served() {
    let mut client = Client::anonymous();
    let document = client.get("/openapi/v1.json").await.expect(StatusCode::OK).json();
    assert_eq!(document["openapi"], "3.1.1");
    assert_eq!(client.get("/healthcheck").await.expect(StatusCode::OK).json(), "yay!");
    assert_eq!(client.get("/health").await.expect(StatusCode::OK).text(), "Healthy");
    assert_eq!(client.get("/alive").await.expect(StatusCode::OK).text(), "Healthy");
    assert!(client.get("/nope").await.expect(StatusCode::NOT_FOUND).bytes.is_empty());
}
