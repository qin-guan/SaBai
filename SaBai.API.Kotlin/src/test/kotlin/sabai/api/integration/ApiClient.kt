package sabai.api.integration

import org.assertj.core.api.Assertions.assertThat
import tools.jackson.databind.JsonNode
import tools.jackson.databind.json.JsonMapper
import java.net.CookieManager
import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse

/** A tiny HTTP client that keeps the session cookie, like the C# tests' HttpClient. */
class ApiClient(private val baseUrl: String, private val json: JsonMapper) {
    private val http = HttpClient.newBuilder().cookieHandler(CookieManager()).build()

    data class Response(val status: Int, val body: JsonNode?, val headers: Map<String, List<String>>) {
        operator fun get(field: String): JsonNode = body!!.path(field)
    }

    fun get(path: String) = send("GET", path, null)

    fun post(path: String, body: Any? = null) = send("POST", path, body)

    fun patch(path: String, body: Any) = send("PATCH", path, body)

    fun delete(path: String) = send("DELETE", path, null)

    fun login(userName: String, password: String = "Password1!") {
        val response = post("/auth/login", mapOf("userName" to userName, "password" to password))
        assertThat(response.status).describedAs("login %s: %s", userName, response.body).isEqualTo(200)
    }

    private fun send(method: String, path: String, body: Any?): Response {
        val request =
            HttpRequest.newBuilder(URI.create(baseUrl + path))
                .header("Content-Type", "application/json")
                .method(
                    method,
                    body?.let { HttpRequest.BodyPublishers.ofString(json.writeValueAsString(it)) } ?: HttpRequest.BodyPublishers.noBody(),
                ).build()
        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        val parsed = response.body().takeIf { it.isNotBlank() }?.let { json.readTree(it) }
        return Response(response.statusCode(), parsed, response.headers().map())
    }
}

/** Values of a JSON array, typed so AssertJ picks its list assertions. */
fun JsonNode.strings(field: String? = null): List<String> = values().map { (if (field == null) it else it.path(field)).asString() }

fun JsonNode.ints(field: String): List<Int> = values().map { it.path(field).asInt() }
