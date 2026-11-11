package sabai.api.auth

import org.springframework.security.crypto.password.PasswordEncoder
import java.nio.ByteBuffer
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.PBEKeySpec

/**
 * Reads and writes ASP.NET Core Identity password hashes, so accounts created by the
 * C# API keep working (and vice versa).
 *
 * V3 layout: `0x01 | prf (u32 BE) | iterations (u32 BE) | salt length (u32 BE) | salt | subkey`,
 * base64-encoded. V2 (`0x00 | salt[16] | subkey[32]`, PBKDF2-SHA1 x1000) is still verified.
 */
object IdentityPasswordHasher : PasswordEncoder {
    private const val ITERATIONS = 100_000
    private const val SALT_BYTES = 16
    private const val SUBKEY_BYTES = 32
    private const val PRF_SHA512 = 2
    private val random = SecureRandom()

    override fun encode(rawPassword: CharSequence?): String {
        val salt = ByteArray(SALT_BYTES).also(random::nextBytes)
        val subkey = pbkdf2(PRF_SHA512, rawPassword.toString(), salt, ITERATIONS, SUBKEY_BYTES)
        val bytes =
            ByteBuffer.allocate(13 + salt.size + subkey.size)
                .put(0x01)
                .putInt(PRF_SHA512)
                .putInt(ITERATIONS)
                .putInt(salt.size)
                .put(salt)
                .put(subkey)
                .array()
        return Base64.getEncoder().encodeToString(bytes)
    }

    override fun matches(rawPassword: CharSequence?, encodedPassword: String?): Boolean {
        val bytes = runCatching { Base64.getDecoder().decode(encodedPassword.orEmpty()) }.getOrNull() ?: return false
        val password = rawPassword.toString()
        return when (bytes.firstOrNull()) {
            0x00.toByte() -> bytes.size == 49 && verify(password, bytes.copyOfRange(1, 17), 0, 1000, bytes.copyOfRange(17, 49))
            0x01.toByte() -> {
                if (bytes.size < 13) return false
                val buffer = ByteBuffer.wrap(bytes, 1, bytes.size - 1)
                val prf = buffer.int
                val iterations = buffer.int
                val saltLength = buffer.int
                if (saltLength < 16 || bytes.size - 13 - saltLength < 16) return false
                val salt = ByteArray(saltLength).also(buffer::get)
                val expected = ByteArray(buffer.remaining()).also(buffer::get)
                verify(password, salt, prf, iterations, expected)
            }
            else -> false
        }
    }

    /** Re-hash on next sign-in when the stored hash is weaker than what we would write today. */
    override fun upgradeEncoding(encodedPassword: String?): Boolean {
        val bytes = runCatching { Base64.getDecoder().decode(encodedPassword.orEmpty()) }.getOrNull() ?: return true
        if (bytes.firstOrNull() != 0x01.toByte() || bytes.size < 9) return true
        val buffer = ByteBuffer.wrap(bytes, 1, 8)
        return buffer.int != PRF_SHA512 || buffer.int < ITERATIONS
    }

    private fun verify(password: String, salt: ByteArray, prf: Int, iterations: Int, expected: ByteArray): Boolean =
        MessageDigest.isEqual(pbkdf2(prf, password, salt, iterations, expected.size), expected)

    private fun pbkdf2(prf: Int, password: String, salt: ByteArray, iterations: Int, length: Int): ByteArray {
        val algorithm =
            when (prf) {
                0 -> "PBKDF2WithHmacSHA1"
                1 -> "PBKDF2WithHmacSHA256"
                2 -> "PBKDF2WithHmacSHA512"
                else -> error("Unknown PRF $prf")
            }
        // The JDK encodes the password as UTF-8, like KeyDerivation.Pbkdf2 in .NET.
        val spec = PBEKeySpec(password.toCharArray(), salt, iterations, length * 8)
        return SecretKeyFactory.getInstance(algorithm).generateSecret(spec).encoded
    }
}
