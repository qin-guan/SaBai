package sabai.api.endpoints.patients.get

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.PathVariable
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.endpoints.patients.PatientReader
import sabai.api.endpoints.patients.PatientResponse

@RestController
class Endpoint(private val reader: PatientReader) {
    @Operation(summary = "Get a patient")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/patients/{id}")
    fun handle(@PathVariable id: Int): PatientResponse = reader.get(id)
}
