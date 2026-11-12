use reqwest::StatusCode;
use serde_json::{Value, json};

use crate::support::*;

fn ids(list: &Value) -> Vec<i64> {
    list.as_array().unwrap().iter().map(id).collect()
}

#[tokio::test]
async fn create_then_publish_sets_published_at() {
    let mut client = Client::admin().await;
    let title = format!("Broadcast {}", suffix());

    let created = client
        .post("/broadcasts", json!({ "title": title, "body": "Clinic is closing early.", "severity": "warning" }))
        .await
        .expect(StatusCode::CREATED);
    let broadcast = created.json();
    let path = format!("/broadcasts/{}/publish", id(&broadcast));
    let published = client.post_empty(&path).await.expect(StatusCode::OK).json();
    let again = client.post_empty(&path).await.expect(StatusCode::OK).json();
    let listed = client.get("/broadcasts").await.expect(StatusCode::OK).json();

    assert_eq!(created.header("location"), Some("/broadcasts"));
    assert_eq!(broadcast["publishedAt"], Value::Null);
    assert_eq!(broadcast["createdBy"], ADMIN);
    assert_eq!(broadcast["severity"], "warning");
    assert_eq!(broadcast["attempts"], 0);
    assert!(published["publishedAt"].is_string());
    assert_eq!(again["publishedAt"], published["publishedAt"]);
    assert!(listed.as_array().unwrap().iter().any(|item| item == &published && item["title"] == title.as_str()));
}

#[tokio::test]
async fn list_limit_and_since_page_newest_first() {
    let mut client = Client::admin().await;
    let mut created = Vec::new();
    for index in 0..3 {
        let broadcast = client
            .post(
                "/broadcasts",
                json!({ "title": format!("Page {index} {}", suffix()), "body": "paging", "severity": "info" }),
            )
            .await
            .expect(StatusCode::CREATED)
            .json();
        created.push(id(&broadcast));
    }

    let after_first =
        client.get(&format!("/broadcasts?since={}&limit=200", created[0])).await.expect(StatusCode::OK).json();
    let newest_two = client.get("/broadcasts?limit=2").await.expect(StatusCode::OK).json();
    let too_many = client.get("/broadcasts?limit=201").await.expect(StatusCode::BAD_REQUEST);
    let zero = client.get("/broadcasts?limit=0").await.expect(StatusCode::BAD_REQUEST);

    // Other tests create broadcasts too, so only this test's own are compared.
    let after_first = ids(&after_first);
    let mine: Vec<i64> = after_first.iter().copied().filter(|id| created.contains(id)).collect();
    assert_eq!(mine, [created[2], created[1]]);
    assert!(after_first.iter().all(|id| *id > created[0]));
    assert!(after_first.windows(2).all(|pair| pair[0] > pair[1]));
    let newest_two = ids(&newest_two);
    assert_eq!(newest_two.len(), 2);
    assert!(newest_two[0] > newest_two[1] && newest_two[1] >= created[1]);
    assert_eq!(
        too_many.errors(),
        [("limit".to_owned(), "'limit' must be between 1 and 200. You entered 201.".to_owned())]
    );
    assert_eq!(zero.errors(), [("limit".to_owned(), "'limit' must be between 1 and 200. You entered 0.".to_owned())]);
}

#[tokio::test]
async fn create_list_contains_title_and_admin_creator() {
    let mut client = Client::admin().await;
    let title = format!("Alert {}", suffix());

    let broadcast = client
        .post("/broadcasts", json!({ "title": format!("  {title} "), "body": " Clinic is closing early. " }))
        .await
        .expect(StatusCode::CREATED)
        .json();
    let listed = client.get("/broadcasts").await.expect(StatusCode::OK);

    assert_eq!(broadcast["createdBy"], ADMIN);
    assert_eq!(broadcast["title"], title.as_str());
    assert_eq!(broadcast["body"], "Clinic is closing early.");
    assert_eq!(broadcast["severity"], "info");
    assert!(listed.text().contains(&title));
}

#[tokio::test]
async fn create_empty_title_returns_bad_request() {
    let mut client = Client::admin().await;

    let response = client
        .post("/broadcasts", json!({ "title": "", "body": format!("Empty title {}", suffix()), "severity": "info" }))
        .await
        .expect(StatusCode::BAD_REQUEST);

    assert_eq!(response.errors(), [("title".to_owned(), "'title' must not be empty.".to_owned())]);
}

#[tokio::test]
async fn publish_unknown_id_returns_not_found() {
    let mut client = Client::admin().await;

    let response = client.post_empty("/broadcasts/99999999/publish").await.expect(StatusCode::NOT_FOUND);

    assert_eq!(response.detail(), "Broadcast was not found.");
}

#[tokio::test]
async fn delete_removes_title_from_list() {
    let mut client = Client::signed_in(DOCTOR).await;
    let title = format!("Remove {}", suffix());
    let created = client
        .post("/broadcasts", json!({ "title": title, "body": "This notice will be removed.", "severity": "critical" }))
        .await
        .expect(StatusCode::CREATED)
        .json();
    let path = format!("/broadcasts/{}", id(&created));

    client.delete(&path).await.expect(StatusCode::NO_CONTENT);
    let listed = client.get("/broadcasts").await.expect(StatusCode::OK);
    let again = client.delete(&path).await.expect(StatusCode::NOT_FOUND);

    assert_eq!(created["createdBy"], DOCTOR);
    assert!(!listed.text().contains(&title));
    assert_eq!(again.detail(), "Broadcast was not found.");
}

#[tokio::test]
async fn create_without_session_returns_unauthorized() {
    let mut client = Client::anonymous();

    let response =
        client.post("/broadcasts", json!({ "title": "Closed", "body": "No token", "severity": "info" })).await;

    response.expect(StatusCode::UNAUTHORIZED);
}
