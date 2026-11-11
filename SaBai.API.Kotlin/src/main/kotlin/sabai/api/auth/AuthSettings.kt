package sabai.api.auth

import org.springframework.boot.context.properties.ConfigurationProperties

/** Bound from `sabai.auth.*` (the C# "Auth" section). */
@ConfigurationProperties("sabai.auth")
data class AuthSettings(
    /** How long a sign-in stays valid without activity (sliding). */
    val sessionLifetimeMinutes: Long = 480,
    /** How often a session is re-checked against the database, so role changes, locks and password resets apply. */
    val securityStampValidationMinutes: Long = 1,
    /** Issuer shown in authenticator apps next to the account name. */
    val authenticatorIssuer: String = "Sa'Bai",
    /** Seed the demo staff accounts (development and tests only). */
    val seedDemoUsers: Boolean = false,
    /** Optional first admin, created at startup if missing. */
    val bootstrapAdmin: BootstrapAdmin = BootstrapAdmin(),
    val maxFailedAccessAttempts: Int = 5,
    val lockoutMinutes: Long = 5,
) {
    data class BootstrapAdmin(val userName: String = "", val password: String = "", val email: String? = null)
}
