package sabai.api.web

import org.springframework.http.ResponseEntity
import java.net.URI

/** `Send.CreatedAtAsync<Get.Endpoint>(...)`: 201 with a Location header. */
fun <T : Any> created(location: String, body: T): ResponseEntity<T> = ResponseEntity.created(URI.create(location)).body(body)
