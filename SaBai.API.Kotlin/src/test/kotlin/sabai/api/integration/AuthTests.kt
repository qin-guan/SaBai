package sabai.api.integration

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import sabai.api.auth.Totp
import java.time.Instant

class AuthTests : IntegrationTest() {
    @Test
    fun `endpoints need a session`() {
        assertThat(client().get("/patients").status).isEqualTo(401)
        assertThat(client().get("/auth/whoami").status).isEqualTo(401)
    }

    @Test
    fun `sign in, read the profile and sign out`() {
        val client = client()
        client.login("doctor")

        val me = client.get("/auth/whoami")
        assertThat(me.status).isEqualTo(200)
        assertThat(me["user"]["userName"].asString()).isEqualTo("doctor")
        assertThat(me["user"]["roles"].strings()).containsExactly("member")

        assertThat(client.post("/auth/logout").status).isEqualTo(204)
        assertThat(client.get("/auth/whoami").status).isEqualTo(401)
    }

    @Test
    fun `five wrong passwords lock the account`() {
        val name = createUser()
        val client = client()

        val failures = (1..5).map { client.post("/auth/login", mapOf("userName" to name, "password" to "wrong")).status }
        val correct = client.post("/auth/login", mapOf("userName" to name, "password" to "Password1!"))

        assertThat(failures).containsExactly(401, 401, 401, 401, 403)
        assertThat(correct.status).isEqualTo(403)
        assertThat(correct["detail"].asString()).contains("locked")
    }

    @Test
    fun `two-factor sign-in needs the authenticator code`() {
        val key = Totp.newKey()
        val name = createUser(authenticatorKey = key)
        val client = client()

        val first = client.post("/auth/login", mapOf("userName" to name, "password" to "Password1!"))
        assertThat(first.status).isEqualTo(200)
        assertThat(first["requiresTwoFactor"].asBoolean()).isTrue()
        assertThat(client.get("/auth/whoami").status).isEqualTo(401)

        val wrong = client.post("/auth/login/two-factor", mapOf("code" to "000000"))
        val code = "%06d".format(Totp.compute(Totp.base32Decode(key), Instant.now().epochSecond / 30))
        val right = client.post("/auth/login/two-factor", mapOf("code" to code))

        assertThat(wrong.status).isEqualTo(401)
        assertThat(right.status).describedAs(right.body.toString()).isEqualTo(200)
        assertThat(client.get("/auth/whoami").status).isEqualTo(200)
    }
}
