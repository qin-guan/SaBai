package sabai.api.integration

import org.junit.jupiter.api.condition.EnabledIf
import org.springframework.beans.factory.annotation.Autowired
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.web.server.LocalServerPort
import org.springframework.test.context.DynamicPropertyRegistry
import org.springframework.test.context.DynamicPropertySource
import org.springframework.transaction.support.TransactionTemplate
import org.testcontainers.DockerClientFactory
import org.testcontainers.postgresql.PostgreSQLContainer
import sabai.api.auth.IdentityPasswordHasher
import sabai.api.auth.Totp
import sabai.api.auth.uuidV7
import sabai.api.data.StaffUserRepository
import tools.jackson.databind.json.JsonMapper
import java.time.OffsetDateTime
import java.util.UUID

/**
 * Boots the whole API on a random port against a real Postgres (the AppFixture of the
 * C# tests). Uses Testcontainers when Docker is available, otherwise the database in
 * SABAI_TEST_DB_URL; skipped when neither is there.
 */
@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT,
    properties = ["sabai.auth.seed-demo-users=true", "sabai.face-recognition.warm-up-on-start=false"],
)
@EnabledIf("sabai.api.integration.TestDatabase#available")
abstract class IntegrationTest {
    @LocalServerPort private var port = 0

    @Autowired protected lateinit var json: JsonMapper

    @Autowired protected lateinit var users: StaffUserRepository

    @Autowired protected lateinit var transactions: TransactionTemplate

    protected fun client() = ApiClient("http://localhost:$port", json)

    protected fun signedIn(userName: String = "doctor") = client().also { it.login(userName) }

    /** A unique, short code so tests can share one database. */
    protected fun code() = "T" + UUID.randomUUID().toString().take(6).uppercase()

    /** A fresh staff user with password "Password1!", optionally with an authenticator key. */
    protected fun createUser(authenticatorKey: String? = null): String {
        val name = "user-" + UUID.randomUUID().toString().take(8)
        transactions.executeWithoutResult {
            val user =
                users.save(
                    sabai.api.domain.StaffUser(uuidV7(), name, name, OffsetDateTime.now()).apply {
                        passwordHash = IdentityPasswordHasher.encode("Password1!")
                        securityStamp = Totp.newKey()
                        twoFactorEnabled = authenticatorKey != null
                        roles += users.findByNormalizedUserName("DOCTOR")!!.roles
                    },
                )
            if (authenticatorKey != null) {
                tokens.save(sabai.api.domain.UserToken(user.id, "[AspNetUserStore]", "AuthenticatorKey", authenticatorKey))
            }
        }
        return name
    }

    @Autowired private lateinit var tokens: sabai.api.data.UserTokenRepository

    companion object {
        @JvmStatic
        @DynamicPropertySource
        fun database(registry: DynamicPropertyRegistry) {
            registry.add("spring.datasource.url", TestDatabase::url)
            registry.add("spring.datasource.username", TestDatabase::user)
            registry.add("spring.datasource.password", TestDatabase::password)
        }
    }
}

object TestDatabase {
    private val external: String? = System.getenv("SABAI_TEST_DB_URL")

    private val container by lazy { PostgreSQLContainer("postgres:17-alpine").apply { start() } }

    @JvmStatic
    fun available(): Boolean = external != null || runCatching { DockerClientFactory.instance().isDockerAvailable }.getOrDefault(false)

    fun url(): String = external ?: container.jdbcUrl

    fun user(): String = if (external != null) System.getenv("SABAI_TEST_DB_USER") ?: "postgres" else container.username

    fun password(): String = if (external != null) System.getenv("SABAI_TEST_DB_PASSWORD") ?: "postgres" else container.password
}
