package sabai.api.endpoints.broadcasts.delete

import io.swagger.v3.oas.annotations.Operation
import org.springframework.http.HttpStatus
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.DeleteMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.ResponseStatus
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.BroadcastRepository
import sabai.api.data.findOrNotFound

@RestController
class Endpoint(private val broadcasts: BroadcastRepository) {
    @Operation(summary = "Delete a broadcast")
    @StaffOnly
    @Transactional
    @DeleteMapping("/broadcasts/{id}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    fun handle(@PathVariable id: Int) {
        broadcasts.delete(broadcasts.findOrNotFound(id, "Broadcast was not found."))
    }
}
