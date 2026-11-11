package sabai.api.unit

import org.assertj.core.api.Assertions.assertThat
import org.junit.jupiter.api.Test
import sabai.api.auth.IdentityPasswordHasher

/**
 * The expected hashes were produced independently (Python's hashlib) in ASP.NET Core
 * Identity's byte layout, so passing means hashes written by the C# API verify here.
 */
class IdentityPasswordHasherTest {
    @Test
    fun `verifies an Identity V3 hash (PBKDF2-SHA512, 100k iterations)`() {
        val hash = "AQAAAAIAAYagAAAAEAABAgMEBQYHCAkKCwwNDg+26DSQ6NjVYMKMlaZu4z9DLjWVI/M2yAd0gbRHYyBZlQ=="
        assertThat(IdentityPasswordHasher.matches("Password1!", hash)).isTrue()
        assertThat(IdentityPasswordHasher.matches("password1!", hash)).isFalse()
        assertThat(IdentityPasswordHasher.upgradeEncoding(hash)).isFalse()
    }

    @Test
    fun `verifies older formats and asks for them to be upgraded`() {
        val v2 = "AAABAgMEBQYHCAkKCwwNDg8wZJDd0FRS3KgSJ7e+eT04h+2KlaK8w9OjQFVPz+iXLA=="
        val sha256 = "AQAAAAEAACcQAAAAEAABAgMEBQYHCAkKCwwNDg8osy8Z/hHVMFLKfuG4GNPgYOCGnkbj5bDvCijlawFqWg=="
        assertThat(IdentityPasswordHasher.matches("Password1!", v2)).isTrue()
        assertThat(IdentityPasswordHasher.matches("pässwörd", sha256)).isTrue() // UTF-8 like .NET
        assertThat(IdentityPasswordHasher.upgradeEncoding(v2)).isTrue()
        assertThat(IdentityPasswordHasher.upgradeEncoding(sha256)).isTrue()
    }

    @Test
    fun `round-trips its own hashes and rejects garbage`() {
        val hash = IdentityPasswordHasher.encode("s3cret")
        assertThat(IdentityPasswordHasher.matches("s3cret", hash)).isTrue()
        assertThat(IdentityPasswordHasher.matches("other", hash)).isFalse()
        assertThat(IdentityPasswordHasher.matches("s3cret", "not base64!")).isFalse()
        assertThat(IdentityPasswordHasher.matches("s3cret", "")).isFalse()
    }
}
