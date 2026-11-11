package sabai.api.auth

import java.nio.ByteBuffer
import java.security.SecureRandom
import java.time.Instant
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

/**
 * RFC 6238 codes as checked by Identity's AuthenticatorTokenProvider: HMAC-SHA1,
 * 30-second steps, 6 digits, accepting two steps either side for clock drift.
 */
object Totp {
    private const val STEP_SECONDS = 30L
    private const val ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"

    fun verify(base32Key: String, code: String, now: Instant): Boolean {
        val expected = code.takeIf { it.isNotEmpty() && it.all(Char::isDigit) }?.toIntOrNull() ?: return false
        val key = base32Decode(base32Key)
        val step = Math.round(now.toEpochMilli() / 1000.0) / STEP_SECONDS
        return (-2..2).any { compute(key, step + it) == expected }
    }

    fun compute(key: ByteArray, step: Long, digits: Int = 6): Int {
        val mac = Mac.getInstance("HmacSHA1").apply { init(SecretKeySpec(key, "HmacSHA1")) }
        val hash = mac.doFinal(ByteBuffer.allocate(8).putLong(step).array())
        val offset = hash.last().toInt() and 0x0f
        val binary = ByteBuffer.wrap(hash, offset, 4).int and 0x7fffffff
        return binary % Math.pow(10.0, digits.toDouble()).toInt()
    }

    /** A new random key in the same 32-character format Identity generates. */
    fun newKey(): String = base32Encode(ByteArray(20).also(SecureRandom()::nextBytes))

    fun base32Encode(bytes: ByteArray): String = buildString {
        var buffer = 0
        var bits = 0
        for (byte in bytes) {
            buffer = (buffer shl 8) or (byte.toInt() and 0xff)
            bits += 8
            while (bits >= 5) {
                append(ALPHABET[(buffer shr (bits - 5)) and 31])
                bits -= 5
            }
        }
        if (bits > 0) append(ALPHABET[(buffer shl (5 - bits)) and 31])
    }

    fun base32Decode(text: String): ByteArray {
        val clean = text.uppercase().filter { it in ALPHABET }
        val out = ByteArray(clean.length * 5 / 8)
        var buffer = 0
        var bits = 0
        var index = 0
        for (char in clean) {
            buffer = (buffer shl 5) or ALPHABET.indexOf(char)
            bits += 5
            if (bits >= 8) {
                out[index++] = (buffer shr (bits - 8)).toByte()
                bits -= 8
            }
        }
        return out
    }
}
