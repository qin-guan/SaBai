package sabai.api.endpoints.patients.list

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.endpoints.patients.PatientReader
import sabai.api.endpoints.patients.PatientResponse

/** Optional name and village-code filters, from the query string. */
data class Request(val name: String? = null, val villageCode: String? = null)

/** Patients ordered by id. */
data class Response(val patients: List<PatientResponse>)

@RestController
class Endpoint(private val reader: PatientReader) {
    @Operation(summary = "List patients")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/patients")
    fun handle(req: Request): Response =
        Response(reader.list(name = req.name?.trim()?.ifEmpty { null }, villageCode = req.villageCode?.trim()?.ifEmpty { null }))
}
