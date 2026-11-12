use reqwest::StatusCode;
use serde_json::json;

use crate::support::*;

#[tokio::test]
async fn village_code_can_be_created_read_updated_and_deleted() {
    let mut client = Client::admin().await;
    let code = village_code();

    let created = client
        .post("/village-codes", json!({ "code": format!(" {code} "), "name": " Alpha ", "colorHex": "#fff" }))
        .await
        .expect(StatusCode::CREATED);
    let village = created.json();
    let path = format!("/village-codes/{}", id(&village));
    assert_eq!(created.header("location"), Some(path.as_str()));
    assert_eq!(village["code"], code);
    assert_eq!(village["name"], "Alpha");
    assert_eq!(village["isVisible"], true);
    assert_eq!(village["createdAt"], village["updatedAt"]);

    let fetched = client.get(&path).await.expect(StatusCode::OK).json();
    assert_eq!(fetched, village);

    let updated = client.patch(&path, json!({ "name": "Beta", "isVisible": false, "id": 0 })).await.ok().json();
    assert_eq!(updated["name"], "Beta");
    assert_eq!(updated["code"], code);
    assert_eq!(updated["isVisible"], false);
    assert_ne!(updated["updatedAt"], village["updatedAt"]);

    let visible = client.get("/village-codes").await.ok().json();
    assert!(!visible["villageCodes"].as_array().unwrap().iter().any(|v| v["code"] == code));
    let all = client.get("/village-codes?includeHidden=true").await.ok().json();
    assert!(all["villageCodes"].as_array().unwrap().iter().any(|v| v["code"] == code));

    client.delete(&path).await.expect(StatusCode::NO_CONTENT);
    let gone = client.get(&path).await.expect(StatusCode::NOT_FOUND);
    assert_eq!(gone.header("content-type"), Some("application/json; charset=utf-8"));
    assert_eq!(
        gone.json(),
        json!({ "title": "Not Found", "status": 404, "detail": "Village code was not found.", "instance": path })
    );
}

#[tokio::test]
async fn duplicate_code_is_a_conflict() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let other = create_village(&mut client).await;

    let duplicate = client
        .post("/village-codes", json!({ "code": village["code"], "name": "Again", "colorHex": "#336699" }))
        .await
        .expect(StatusCode::CONFLICT);
    assert_eq!(duplicate.detail(), "A village code with this code already exists.");

    let renamed = client
        .patch(&format!("/village-codes/{}", id(&other)), json!({ "code": village["code"] }))
        .await
        .expect(StatusCode::CONFLICT);
    assert_eq!(renamed.detail(), "A village code with this code already exists.");
}

#[tokio::test]
async fn invalid_requests_are_rejected_with_the_reasons() {
    let mut client = Client::admin().await;

    let invalid = client
        .post("/village-codes", json!({ "code": "", "name": "x".repeat(256), "colorHex": "blue" }))
        .await
        .expect(StatusCode::BAD_REQUEST);
    assert_eq!(invalid.header("content-type"), Some("application/problem+json; charset=utf-8"));
    let body = invalid.json();
    assert_eq!(body["type"], "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1");
    assert_eq!(body["title"], "Bad Request");
    assert_eq!(body["instance"], "/village-codes");
    assert!(body.get("detail").is_none(), "several failures carry no detail");
    assert_eq!(
        invalid.errors(),
        [
            ("code".to_owned(), "'code' must not be empty.".to_owned()),
            (
                "name".to_owned(),
                "The length of 'name' must be 255 characters or fewer. You entered 256 characters.".to_owned()
            ),
            ("colorHex".to_owned(), "'color Hex' is not in the correct format.".to_owned()),
        ]
    );

    let missing = client.post("/village-codes", json!({ "code": "A", "name": "A" })).await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(missing.errors()[0].0, "serializerErrors");
    assert!(missing.detail().contains("'colorHex'"));

    let not_a_number = client.get("/village-codes/abc").await.expect(StatusCode::BAD_REQUEST);
    assert_eq!(
        not_a_number.errors(),
        [("id".to_owned(), "Value [abc] is not valid for a [Int32] property!".to_owned())]
    );
    assert_eq!(not_a_number.detail(), "Value [abc] is not valid for a [Int32] property!");
}

#[tokio::test]
async fn requests_are_read_leniently() {
    let mut client = Client::admin().await;
    let code = village_code();

    // Property names in any case, booleans as text, unknown properties ignored.
    let created = client
        .post("/village-codes", json!({ "Code": code, "NAME": "Alpha", "colorHex": "#fff", "isVisible": "false", "extra": 1 }))
        .await
        .expect(StatusCode::CREATED)
        .json();
    assert_eq!(created["isVisible"], false);

    // The route wins over an id in the body.
    let path = format!("/village-codes/{}", id(&created));
    let updated = client.patch(&path, json!({ "id": 999999, "name": "Renamed" })).await.ok().json();
    assert_eq!(updated["id"], created["id"]);
}

#[tokio::test]
async fn signed_out_requests_are_unauthorized() {
    let mut client = Client::anonymous();
    let response = client.get("/village-codes").await.expect(StatusCode::UNAUTHORIZED);
    assert!(response.bytes.is_empty());
}

#[tokio::test]
async fn created_village_is_returned_and_its_code_cannot_be_reused() {
    let mut client = Client::admin().await;
    let code = suffix()[..6].to_owned();

    let created = client
        .post("/village-codes", json!({ "code": code, "name": format!("Village {code}"), "colorHex": "#A1B2C3", "isVisible": true }))
        .await
        .expect(StatusCode::CREATED);
    let village = created.json();
    let duplicate = client.post("/village-codes", json!({ "code": code, "name": "Other", "colorHex": "#111111" })).await;

    assert_eq!(created.header("location"), Some(format!("/village-codes/{}", id(&village)).as_str()));
    assert_eq!(village["code"], code);
    assert_eq!(village["colorHex"], "#A1B2C3");
    assert_eq!(village["isVisible"], true);
    assert_eq!(duplicate.status, StatusCode::CONFLICT);
}

#[tokio::test]
async fn list_hides_invisible_codes_unless_requested() {
    let mut client = Client::admin().await;
    let visible = create_village(&mut client).await;
    let hidden_code = village_code();
    client
        .post("/village-codes", json!({ "code": hidden_code, "name": "Hidden", "colorHex": "#ABC", "isVisible": false }))
        .await
        .ok();

    let visible_only = client.get("/village-codes").await.ok().json();
    let including_hidden = client.get("/village-codes?includeHidden=true").await.ok().json();

    let visible_only = visible_only["villageCodes"].as_array().unwrap();
    assert!(visible_only.iter().any(|x| x["code"] == visible["code"]));
    assert!(!visible_only.iter().any(|x| x["code"] == hidden_code));
    let including_hidden = including_hidden["villageCodes"].as_array().unwrap();
    assert!(including_hidden.iter().any(|x| x["code"] == hidden_code && x["colorHex"] == "#ABC"));
}

#[tokio::test]
async fn village_with_a_visit_cannot_be_deleted() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    register_patient(&mut client, id(&village)).await;
    let path = format!("/village-codes/{}", id(&village));

    let deleted = client.delete(&path).await.expect(StatusCode::CONFLICT);
    let still_there = client.get(&path).await;

    assert_eq!(deleted.detail(), "A visit still references this village code.");
    assert_eq!(still_there.status, StatusCode::OK);
}

#[tokio::test]
async fn hidden_village_can_still_be_fetched() {
    let mut client = Client::admin().await;
    let village = create_village(&mut client).await;
    let path = format!("/village-codes/{}", id(&village));
    client.patch(&path, json!({ "isVisible": false })).await.ok();

    let body = client.get(&path).await.expect(StatusCode::OK).json();

    assert_eq!(body["code"], village["code"]);
    assert_eq!(body["isVisible"], false);
}

#[tokio::test]
async fn invalid_color_is_rejected() {
    let mut client = Client::admin().await;
    let code = village_code();

    let response = client.post("/village-codes", json!({ "code": code, "name": format!("Village {code}"), "colorHex": "red" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn empty_name_is_rejected() {
    let mut client = Client::admin().await;

    let response = client.post("/village-codes", json!({ "code": village_code(), "name": "", "colorHex": "#336699" })).await;

    assert_eq!(response.status, StatusCode::BAD_REQUEST);
}

#[tokio::test]
async fn missing_village_is_not_found() {
    let mut client = Client::admin().await;

    let fetched = client.get("/village-codes/99999999").await;
    let patched = client.patch("/village-codes/99999999", json!({ "name": "Missing" })).await;
    let deleted = client.delete("/village-codes/99999999").await;

    assert_eq!(fetched.status, StatusCode::NOT_FOUND);
    assert_eq!(patched.status, StatusCode::NOT_FOUND);
    assert_eq!(deleted.status, StatusCode::NOT_FOUND);
}
