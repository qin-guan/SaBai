package sabai.api.auth

import org.springframework.security.access.prepost.PreAuthorize

/** Role names, as stored in AspNetRoles. Seeded at startup. */
object StaffRoles {
    const val ADMIN = "admin"
    const val MEMBER = "member"

    val ALL = listOf(ADMIN, MEMBER)
}

/** `Roles(StaffRoles.Admin, StaffRoles.Member)` in FastEndpoints. */
@Target(AnnotationTarget.FUNCTION, AnnotationTarget.CLASS)
@Retention(AnnotationRetention.RUNTIME)
@PreAuthorize("hasAnyAuthority('admin', 'member')")
annotation class StaffOnly

/** `Roles(StaffRoles.Admin)` in FastEndpoints. */
@Target(AnnotationTarget.FUNCTION, AnnotationTarget.CLASS)
@Retention(AnnotationRetention.RUNTIME)
@PreAuthorize("hasAuthority('admin')")
annotation class AdminOnly
