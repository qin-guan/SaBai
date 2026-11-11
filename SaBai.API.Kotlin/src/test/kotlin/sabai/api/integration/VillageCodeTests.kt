package sabai.api.integration

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test

/** Port of SaBai.Tests/Patients/VillageCodeTests.cs. */
class VillageCodeTests : IntegrationTest() {
    @Test
    fun `create persists the village and rejects a duplicate code`() {
        // Arrange
        val client = signedIn()
        val code = code()

        // Act
        val created = client.post("/village-codes", mapOf("code" to code, "name" to "Village $code", "colorHex" to "#A1B2C3", "isVisible" to true))
        val duplicate = client.post("/village-codes", mapOf("code" to code, "name" to "Other", "colorHex" to "#111111"))

        // Assert
        assertThat(created.status).isEqualTo(201)
        assertThat(created.headers["location"]).containsExactly("/village-codes/${created["id"].asInt()}")
        assertThat(created["code"].asString()).isEqualTo(code)
        assertThat(created["colorHex"].asString()).isEqualTo("#A1B2C3")
        assertThat(created["isVisible"].asBoolean()).isTrue()
        assertThat(duplicate.status).isEqualTo(409)
    }

    @Test
    fun `list hides invisible codes unless requested`() {
        val client = signedIn()
        val visible = code()
        val hidden = code()
        client.post("/village-codes", mapOf("code" to visible, "name" to visible, "colorHex" to "#ABCDEF"))
        client.post("/village-codes", mapOf("code" to hidden, "name" to hidden, "colorHex" to "#ABC", "isVisible" to false))

        fun codes(path: String) = client.get(path)["villageCodes"].strings("code")

        assertThat(codes("/village-codes")).contains(visible).doesNotContain(hidden)
        assertThat(codes("/village-codes?includeHidden=true")).contains(visible, hidden)
    }

    @Test
    fun `invalid input is rejected with field errors`() {
        val response = signedIn().post("/village-codes", mapOf("code" to "", "name" to "x", "colorHex" to "red"))

        assertThat(response.status).isEqualTo(400)
        assertThat(response["errors"].strings("name")).containsExactlyInAnyOrder("code", "colorHex")
    }
}
