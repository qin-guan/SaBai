package sabai.api.endpoints.auth

import sabai.api.auth.SignInResult
import sabai.api.data.AuditTrail
import sabai.api.domain.AuditAction
import sabai.api.domain.AuthRejectedException
import sabai.api.domain.ForbiddenException

/**
 * Result of a sign-in step. On success the session cookie is set and [user] is returned.
 * With two-factor on, [requiresTwoFactor] is true and the client finishes with
 * `POST /auth/login/two-factor`.
 */
data class SignInResponse(val requiresTwoFactor: Boolean, val user: UserResponse?) {
    companion object {
        fun from(result: SignInResult, audit: AuditTrail, method: String): SignInResponse =
            // `when` over a sealed type: forgetting a case is a compile error.
            when (result) {
                is SignInResult.Succeeded -> {
                    audit.record(AuditAction.SignedIn, result.user, details = method)
                    SignInResponse(false, UserResponse.from(result.user))
                }
                is SignInResult.RequiresTwoFactor -> SignInResponse(true, null)
                is SignInResult.LockedOut -> {
                    audit.record(AuditAction.SignInFailed, result.user, details = "$method: account locked")
                    throw ForbiddenException("This account is locked. Try again later or ask an admin to unlock it.")
                }
                is SignInResult.Failed -> {
                    audit.record(AuditAction.SignInFailed, result.user, details = "$method: rejected")
                    throw AuthRejectedException()
                }
            }
    }
}
