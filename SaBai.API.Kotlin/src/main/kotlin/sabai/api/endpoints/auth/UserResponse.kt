package sabai.api.endpoints.auth

import sabai.api.domain.StaffUser
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/** Staff profile returned by authentication and user endpoints. */
data class UserResponse(
    val id: UUID,
    val userName: String,
    val email: String?,
    val nickname: String,
    val roles: List<String>,
    /** True while the account cannot sign in. */
    val isLocked: Boolean,
    /** When the lock ends: year 9999 for an admin lock, minutes ahead after failed sign-ins. */
    val lockoutEnd: OffsetDateTime?,
    val twoFactorEnabled: Boolean,
    /** False for accounts that still need a password set by an admin. */
    val hasPassword: Boolean,
    val createdAt: OffsetDateTime,
) {
    companion object {
        fun from(user: StaffUser): UserResponse {
            val locked = user.isLockedOut(OffsetDateTime.now(ZoneOffset.UTC))
            return UserResponse(
                id = user.id,
                userName = user.userName,
                email = user.email,
                nickname = user.nickname,
                roles = user.roleNames(),
                isLocked = locked,
                lockoutEnd = if (locked) user.lockoutEnd else null,
                twoFactorEnabled = user.twoFactorEnabled,
                hasPassword = user.passwordHash != null,
                createdAt = user.createdAt,
            )
        }
    }
}
