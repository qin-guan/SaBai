package sabai.api.auth

import java.nio.ByteBuffer
import java.security.SecureRandom
import java.util.UUID

private val random = SecureRandom()

/** Time-ordered UUID, like `Guid.CreateVersion7()` in .NET 9+. */
fun uuidV7(): UUID {
    val bytes = ByteArray(16).also(random::nextBytes)
    val millis = System.currentTimeMillis()
    for (i in 0 until 6) bytes[i] = (millis shr (40 - 8 * i)).toByte()
    bytes[6] = ((bytes[6].toInt() and 0x0f) or 0x70).toByte()
    bytes[8] = ((bytes[8].toInt() and 0x3f) or 0x80).toByte()
    val buffer = ByteBuffer.wrap(bytes)
    return UUID(buffer.long, buffer.long)
}
