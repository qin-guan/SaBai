package sabai.api.auth

import org.slf4j.LoggerFactory
import org.springframework.boot.ApplicationArguments
import org.springframework.boot.ApplicationRunner
import org.springframework.stereotype.Component
import org.springframework.transaction.annotation.Transactional
import sabai.api.data.RoleRepository
import sabai.api.data.StaffUserRepository
import sabai.api.domain.Role
import sabai.api.domain.StaffUser
import java.time.Clock
import java.time.OffsetDateTime

/** Startup seeding for roles, the optional bootstrap admin and demo staff. */
@Component
class AuthSeeder(
    private val roles: RoleRepository,
    private val users: StaffUserRepository,
    private val settings: AuthSettings,
    private val clock: Clock,
) : ApplicationRunner {
    private val log = LoggerFactory.getLogger(javaClass)

    @Transactional
    override fun run(args: ApplicationArguments) {
        for (name in StaffRoles.ALL) {
            roles.findByNormalizedName(StaffUser.normalize(name)) ?: roles.save(Role(uuidV7(), name))
        }

        val bootstrap = settings.bootstrapAdmin
        if (bootstrap.userName.isNotBlank() && bootstrap.password.isNotBlank()) {
            val name = bootstrap.userName.trim()
            ensureUser(name, bootstrap.email, name, StaffRoles.ADMIN, bootstrap.password)
        }

        if (settings.seedDemoUsers) {
            for ((email, userName, nickname, role) in DEMO_PEOPLE) {
                ensureUser(userName, email, nickname, role, DEMO_PASSWORD)
            }
        }
    }

    private fun ensureUser(userName: String, email: String?, nickname: String, role: String, password: String) {
        val user =
            users.findByNormalizedUserName(StaffUser.normalize(userName))
                ?: users.save(
                    StaffUser(uuidV7(), userName, nickname, OffsetDateTime.now(clock)).apply {
                        changeEmail(email)
                        emailConfirmed = email != null
                        securityStamp = Totp.newKey()
                        passwordHash = IdentityPasswordHasher.encode(password)
                    },
                ).also { log.info("Seeded staff user {}", userName) }

        if (user.passwordHash == null) {
            user.passwordHash = IdentityPasswordHasher.encode(password)
            log.info("Set the configured password for {}", userName)
        }
        if (user.roles.none { it.name == role }) {
            user.roles += roles.findByNormalizedName(StaffUser.normalize(role))!!
        }
    }

    data class DemoPerson(val email: String, val userName: String, val nickname: String, val role: String)

    companion object {
        const val DEMO_PASSWORD = "Password1!"

        val DEMO_PEOPLE =
            listOf(
                DemoPerson("admin@sabai.test", "admin", "Admin", StaffRoles.ADMIN),
                DemoPerson("doctor@sabai.test", "doctor", "Doctor", StaffRoles.MEMBER),
                DemoPerson("pharmacist@sabai.test", "pharmacist", "Pharmacist", StaffRoles.MEMBER),
                DemoPerson("registration@sabai.test", "registration", "Registration", StaffRoles.MEMBER),
            )
    }
}
