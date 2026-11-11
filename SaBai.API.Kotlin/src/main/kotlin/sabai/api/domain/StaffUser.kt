package sabai.api.domain

import jakarta.persistence.Entity
import jakarta.persistence.FetchType
import jakarta.persistence.Id
import jakarta.persistence.IdClass
import jakarta.persistence.JoinColumn
import jakarta.persistence.JoinTable
import jakarta.persistence.ManyToMany
import jakarta.persistence.Table
import java.io.Serializable
import java.time.OffsetDateTime
import java.util.UUID

/**
 * Staff account, stored in the same columns ASP.NET Core Identity uses so the C# and
 * Kotlin APIs can share one database (and one set of password hashes).
 */
@Entity
@Table(name = "StaffUsers")
class StaffUser(
    @Id var id: UUID,
    var userName: String,
    var nickname: String,
    var createdAt: OffsetDateTime,
) {
    var normalizedUserName: String = normalize(userName)
    var email: String? = null
    var normalizedEmail: String? = null
    var emailConfirmed: Boolean = false
    var passwordHash: String? = null
    var securityStamp: String? = null
    var concurrencyStamp: String? = UUID.randomUUID().toString()
    var phoneNumber: String? = null
    var phoneNumberConfirmed: Boolean = false
    var twoFactorEnabled: Boolean = false
    var lockoutEnd: OffsetDateTime? = null
    var lockoutEnabled: Boolean = true
    var accessFailedCount: Int = 0

    @ManyToMany(fetch = FetchType.EAGER)
    @JoinTable(
        name = "AspNetUserRoles",
        joinColumns = [JoinColumn(name = "UserId")],
        inverseJoinColumns = [JoinColumn(name = "RoleId")],
    )
    val roles: MutableSet<Role> = mutableSetOf()

    /** True while locked, whether an admin locked it or failed sign-ins did. */
    fun isLockedOut(now: OffsetDateTime): Boolean = lockoutEnabled && lockoutEnd?.isAfter(now) == true

    fun roleNames(): List<String> = roles.mapNotNull { it.name }.sorted()

    fun changeEmail(value: String?) {
        email = value?.trim()?.ifEmpty { null }
        normalizedEmail = email?.let(::normalize)
    }

    companion object {
        /** Same as Identity's UpperInvariantLookupNormalizer. */
        fun normalize(value: String): String = value.uppercase()
    }
}

@Entity
@Table(name = "AspNetRoles")
class Role(
    @Id var id: UUID,
    var name: String?,
) {
    var normalizedName: String? = name?.let(StaffUser::normalize)
    var concurrencyStamp: String? = UUID.randomUUID().toString()
}

/** Identity's token table: authenticator keys and recovery codes. */
@Entity
@Table(name = "AspNetUserTokens")
@IdClass(UserToken.Key::class)
class UserToken(
    @Id var userId: UUID,
    @Id var loginProvider: String,
    @Id var name: String,
    var value: String?,
) {
    data class Key(var userId: UUID? = null, var loginProvider: String? = null, var name: String? = null) :
        Serializable

    companion object {
        const val IDENTITY_STORE = "[AspNetUserStore]"
        const val AUTHENTICATOR_KEY = "AuthenticatorKey"
    }
}
