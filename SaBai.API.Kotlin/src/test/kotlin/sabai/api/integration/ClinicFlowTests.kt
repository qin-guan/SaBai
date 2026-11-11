package sabai.api.integration

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.jdbc.core.JdbcTemplate

class ClinicFlowTests : IntegrationTest() {
    @Autowired private lateinit var jdbc: JdbcTemplate

    private fun ApiClient.village(): Int =
        post("/village-codes", mapOf("code" to code(), "name" to "Village", "colorHex" to "#ABCDEF"))["id"].asInt()

    private fun ApiClient.register(villageId: Int, name: String = "Ada", id: String? = null) =
        post(
            "/patients",
            mapOf(
                "name" to name,
                "identificationNumber" to id,
                "gender" to "female",
                "dateOfBirth" to "1990-01-02T00:00:00Z",
                "drugAllergy" to "none",
                "villageCodeId" to villageId,
                "vitals" to mapOf("temperature" to 36.6, "systolic" to 120),
            ),
        )

    @Test
    fun `registering a patient opens a visit and records vitals`() {
        val client = signedIn()
        val village = client.village()

        val created = client.register(village, name = "Registration ${code()}")
        val id = created["id"].asInt()
        val fetched = client.get("/patients/$id")

        assertThat(created.status).isEqualTo(201)
        assertThat(created["visitId"].asInt()).isPositive()
        assertThat(created["vitalsId"].asInt()).isPositive()
        assertThat(fetched["displayId"].asString()).endsWith("%04d".format(id))
        assertThat(fetched["lastVisitId"].asInt()).isEqualTo(created["visitId"].asInt())
        assertThat(client.get("/patients?name=registration")["patients"].ints("id")).contains(id)
    }

    @Test
    fun `identification numbers must stay unique on update`() {
        val client = signedIn()
        val village = client.village()
        val taken = code()
        client.register(village, id = taken)
        val other = client.register(village)["id"].asInt()

        val conflict = client.patch("/patients/$other", mapOf("identificationNumber" to taken))
        val blank = client.patch("/patients/$other", mapOf("name" to "  "))

        assertThat(conflict.status).isEqualTo(409)
        assertThat(blank.status).isEqualTo(400)
    }

    @Test
    fun `changes are audited with the signed-in user`() {
        val client = signedIn("registration")
        val village = client.village()
        val patient = client.register(village)["id"].asInt()
        client.patch("/patients/$patient", mapOf("contactNo" to "555-0101"))

        val rows =
            jdbc.queryForList(
                """SELECT "Action", "UserName", "Changes"::text AS changes FROM "AuditLogs"
                   WHERE "EntityType" = 'Patient' AND "EntityId" = ? ORDER BY "Id"""",
                patient.toString(),
            )
        assertThat(rows.map { it["Action"] }).containsExactly("Created", "Updated")
        assertThat(rows.map { it["UserName"] }).containsOnly("registration")
        assertThat(rows[1]["changes"] as String).contains("\"Field\": \"ContactNo\"", "\"New\": \"555-0101\"")
    }

    @Test
    fun `consults can be edited for 24 hours, then lock`() {
        val client = signedIn()
        val visit = client.register(client.village())["visitId"].asInt()
        val consult =
            client.post("/consults", mapOf("visitId" to visit, "diagnoses" to listOf(mapOf("details" to "URTI", "category" to "Ear Nose Throat"))))
        val id = consult["id"].asInt()

        val edited = client.patch("/consults/$id", mapOf("remarks" to "review in a week"))
        jdbc.update("""UPDATE "Consults" SET "Date" = now() - interval '25 hours' WHERE "Id" = ?""", id)
        val locked = client.patch("/consults/$id", mapOf("remarks" to "too late"))
        val fetched = client.get("/consults/$id")

        assertThat(consult.status).isEqualTo(201)
        assertThat(consult["diagnoses"][0]["category"].asString()).isEqualTo("Ear Nose Throat")
        assertThat(edited["remarks"].asString()).isEqualTo("review in a week")
        assertThat(locked.status).isEqualTo(405)
        assertThat(fetched["remarks"].asString()).isEqualTo("review in a week")
    }
}
