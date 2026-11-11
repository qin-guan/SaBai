package sabai.api.unit

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import sabai.api.auth.Totp
import java.time.Instant

class TotpTest {
    @Test
    fun `matches the RFC 6238 SHA-1 test vectors`() {
        val key = "12345678901234567890".toByteArray()
        // RFC 6238 appendix B gives 8 digits; Identity uses the last 6.
        assertThat(Totp.compute(key, 59L / 30, digits = 8)).isEqualTo(94287082)
        assertThat(Totp.compute(key, 1111111109L / 30, digits = 8)).isEqualTo(7081804)
        assertThat(Totp.compute(key, 59L / 30)).isEqualTo(287082)
    }

    @Test
    fun `accepts two steps of clock drift either side, like Identity`() {
        val base32 = Totp.newKey()
        val key = Totp.base32Decode(base32)
        val now = Instant.parse("2026-11-11T10:00:00Z")
        val step = now.epochSecond / 30
        fun code(offset: Long) = "%06d".format(Totp.compute(key, step + offset))

        assertThat(Totp.verify(base32, code(0), now)).isTrue()
        assertThat(Totp.verify(base32, code(-2), now)).isTrue()
        assertThat(Totp.verify(base32, code(2), now)).isTrue()
        assertThat(Totp.verify(base32, code(3), now)).isFalse()
        assertThat(Totp.verify(base32, "abc123", now)).isFalse()
    }

    @Test
    fun `base32 round-trips`() {
        val bytes = ByteArray(20) { it.toByte() }
        assertThat(Totp.base32Decode(Totp.base32Encode(bytes))).isEqualTo(bytes)
        assertThat(Totp.newKey()).hasSize(32)
    }
}
