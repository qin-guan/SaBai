package sabai.api.data

import com.linecorp.kotlinjdsl.support.spring.data.jpa.repository.KotlinJdslJpqlExecutor
import org.springframework.data.jpa.repository.EntityGraph
import org.springframework.data.jpa.repository.JpaRepository
import org.springframework.data.repository.CrudRepository
import org.springframework.data.repository.findByIdOrNull
import sabai.api.domain.Broadcast
import sabai.api.domain.Consult
import sabai.api.domain.MedicationBrand
import sabai.api.domain.NotFoundException
import sabai.api.domain.Patient
import sabai.api.domain.Role
import sabai.api.domain.StaffUser
import sabai.api.domain.UserToken
import sabai.api.domain.VillageCode
import sabai.api.domain.Visit
import java.util.UUID

// Spring Data writes the implementation of every method below from its name.
// KotlinJdslJpqlExecutor adds type-safe, LINQ-style queries (see the patient list).

interface VillageCodeRepository : JpaRepository<VillageCode, Int>, KotlinJdslJpqlExecutor {
    fun existsByCode(code: String): Boolean

    fun existsByCodeAndIdNot(code: String, id: Int): Boolean
}

interface PatientRepository : JpaRepository<Patient, Int>, KotlinJdslJpqlExecutor {
    fun existsByIdentificationNumberAndIdNot(identificationNumber: String, id: Int): Boolean
}

interface VisitRepository : JpaRepository<Visit, Int>, KotlinJdslJpqlExecutor {
    fun existsByPatientId(patientId: Int): Boolean

    fun existsByVillageCodeId(villageCodeId: Int): Boolean
}

interface ConsultRepository : JpaRepository<Consult, Int> {
    /** Like `.Include(x => x.Doctor).Include(x => x.Diagnoses)`. */
    @EntityGraph(attributePaths = ["doctor", "diagnoses"])
    fun findWithDoctorAndDiagnosesById(id: Int): Consult?

    @EntityGraph(attributePaths = ["doctor", "diagnoses", "visit.patient.villageCode", "visit.villageCode"])
    fun findForReportById(id: Int): Consult?
}

interface MedicationBrandRepository : JpaRepository<MedicationBrand, Int> {
    fun countByIdIn(ids: Collection<Int>): Long
}

interface BroadcastRepository : JpaRepository<Broadcast, Int>, KotlinJdslJpqlExecutor

interface StaffUserRepository : JpaRepository<StaffUser, UUID> {
    fun findByNormalizedUserName(normalizedUserName: String): StaffUser?

    fun findByNormalizedEmail(normalizedEmail: String): StaffUser?
}

interface RoleRepository : JpaRepository<Role, UUID> {
    fun findByNormalizedName(normalizedName: String): Role?
}

interface UserTokenRepository : JpaRepository<UserToken, UserToken.Key> {
    fun findByUserIdAndLoginProviderAndName(userId: UUID, loginProvider: String, name: String): UserToken?
}

/** `FirstOrNotFoundAsync(x => x.Id == id, message)` from the C# API. */
fun <T : Any, ID : Any> CrudRepository<T, ID>.findOrNotFound(id: ID, message: String): T =
    findByIdOrNull(id) ?: throw NotFoundException(message)
