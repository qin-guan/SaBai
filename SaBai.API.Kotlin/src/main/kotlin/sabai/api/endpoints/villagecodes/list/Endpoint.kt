package sabai.api.endpoints.villagecodes.list

import io.swagger.v3.oas.annotations.Operation
import org.springframework.transaction.annotation.Transactional
import org.springframework.web.bind.annotation.GetMapping
import org.springframework.web.bind.annotation.RestController
import sabai.api.auth.StaffOnly
import sabai.api.data.VillageCodeRepository
import sabai.api.domain.VillageCode
import sabai.api.endpoints.villagecodes.VillageCodeResponse
import sabai.api.endpoints.villagecodes.toResponse

/** Optional filter, bound from the query string (`?includeHidden=true`). */
data class Request(val includeHidden: Boolean = false)

/** Village codes, newest first. Hidden codes are omitted unless requested. */
data class Response(val villageCodes: List<VillageCodeResponse>)

@RestController
class Endpoint(private val villageCodes: VillageCodeRepository) {
    @Operation(summary = "List village codes")
    @StaffOnly
    @Transactional(readOnly = true)
    @GetMapping("/village-codes")
    fun handle(req: Request): Response {
        val codes =
            villageCodes.findAll {
                select(entity(VillageCode::class))
                    .from(entity(VillageCode::class))
                    .whereAnd(if (req.includeHidden) null else path(VillageCode::isVisible).eq(true))
                    .orderBy(path(VillageCode::createdAt).desc(), path(VillageCode::id).desc())
            }
        return Response(codes.filterNotNull().map { it.toResponse() })
    }
}
