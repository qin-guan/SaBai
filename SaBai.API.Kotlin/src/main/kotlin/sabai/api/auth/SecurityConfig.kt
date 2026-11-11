package sabai.api.auth

import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.http.HttpStatus
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity
import org.springframework.security.config.annotation.web.builders.HttpSecurity
import org.springframework.security.config.annotation.web.invoke
import org.springframework.security.web.SecurityFilterChain
import org.springframework.security.web.authentication.HttpStatusEntryPoint
import org.springframework.security.web.context.HttpSessionSecurityContextRepository
import org.springframework.security.web.context.SecurityContextHolderFilter
import org.springframework.security.web.context.SecurityContextRepository
import org.springframework.security.web.savedrequest.NullRequestCache
import org.springframework.session.web.http.CookieSerializer
import org.springframework.session.web.http.DefaultCookieSerializer
import org.springframework.session.security.web.authentication.SpringSessionRememberMeServices
import org.springframework.transaction.support.TransactionTemplate
import org.springframework.web.cors.CorsConfiguration
import org.springframework.web.cors.CorsConfigurationSource
import org.springframework.web.cors.UrlBasedCorsConfigurationSource
import sabai.api.data.StaffUserRepository
import java.time.Clock

/**
 * Cookie sessions, as in the C# API. The session itself lives in Postgres (Spring
 * Session JDBC); the browser only holds an HttpOnly, SameSite=Lax id cookie.
 */
@Configuration
@EnableMethodSecurity
@EnableConfigurationProperties(AuthSettings::class, CorsSettings::class)
class SecurityConfig {
    @Bean
    fun securityContextRepository(): SecurityContextRepository = HttpSessionSecurityContextRepository()

    @Bean
    fun filterChain(
        http: HttpSecurity,
        contexts: SecurityContextRepository,
        users: StaffUserRepository,
        transactions: TransactionTemplate,
        settings: AuthSettings,
        clock: Clock,
    ): SecurityFilterChain {
        http {
            // The web apps call the API through a same-origin proxy with a SameSite=Lax
            // cookie, the same posture as the C# API (which has no antiforgery either).
            csrf { disable() }
            cors { }
            securityContext { securityContextRepository = contexts }
            requestCache { requestCache = NullRequestCache() }
            authorizeHttpRequests {
                authorize("/auth/login", permitAll)
                authorize("/auth/login/two-factor", permitAll)
                authorize("/auth/logout", permitAll)
                authorize("/health/**", permitAll)
                authorize("/scalar/**", permitAll)
                authorize("/v3/api-docs/**", permitAll)
                authorize("/error", permitAll)
                // Endpoints narrow this further with @StaffOnly / @AdminOnly.
                authorize(anyRequest, authenticated)
            }
            // An API answers with status codes instead of redirecting to a login page.
            exceptionHandling {
                authenticationEntryPoint = HttpStatusEntryPoint(HttpStatus.UNAUTHORIZED)
                accessDeniedHandler = { _, response, _ -> response.status = HttpStatus.FORBIDDEN.value() }
            }
            addFilterAfter<SecurityContextHolderFilter>(
                SecurityStampFilter(users, contexts, transactions, settings, clock),
            )
        }
        return http.build()
    }

    @Bean
    fun cookieSerializer(): CookieSerializer =
        DefaultCookieSerializer().apply {
            setCookieName(SESSION_COOKIE_NAME)
            setUseHttpOnlyCookie(true)
            setSameSite("Lax")
            setUseBase64Encoding(false)
            setRememberMeRequestAttribute(SpringSessionRememberMeServices.REMEMBER_ME_LOGIN_ATTR)
        }

    @Bean
    fun corsConfigurationSource(cors: CorsSettings): CorsConfigurationSource =
        UrlBasedCorsConfigurationSource().apply {
            registerCorsConfiguration(
                "/**",
                CorsConfiguration().apply {
                    allowedOrigins = cors.allowedOrigins
                    allowedHeaders = listOf("*")
                    allowedMethods = listOf("*")
                    allowCredentials = true
                },
            )
        }

    companion object {
        /** Name of the HttpOnly session cookie, same as the C# API. */
        const val SESSION_COOKIE_NAME = "sabai.auth"
    }
}

@org.springframework.boot.context.properties.ConfigurationProperties("sabai.cors")
data class CorsSettings(val allowedOrigins: List<String> = listOf("http://localhost:3000", "http://localhost:3300"))
