package sabai.api.domain

import jakarta.persistence.CascadeType
import jakarta.persistence.Column
import jakarta.persistence.Entity
import jakarta.persistence.EnumType
import jakarta.persistence.Enumerated
import jakarta.persistence.FetchType
import jakarta.persistence.GeneratedValue
import jakarta.persistence.GenerationType
import jakarta.persistence.Id
import jakarta.persistence.JoinColumn
import jakarta.persistence.ManyToOne
import jakarta.persistence.OneToMany
import jakarta.persistence.Table
import java.time.OffsetDateTime

@Entity
@Table(name = "Patients")
class Patient(
    var name: String,
    @Enumerated(EnumType.STRING) var gender: Gender,
    var dateOfBirth: OffsetDateTime,
    var drugAllergy: String,
    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "VillageCodeId")
    var villageCode: VillageCode?,
    var createdAt: OffsetDateTime,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var identificationNumber: String? = null
    var contactNo: String? = null
    var hasPoorCard: Boolean = false
    var hasBS2Card: Boolean = false
    var hasSabaiCard: Boolean = false
    var toGetReport: Boolean = false

    /** RustFS object key of the profile photo. Mapped to the legacy column name. */
    @Column(name = "PatientImagePublicId")
    var photoStoredFileName: String? = null

    /** Qdrant face id most recently enrolled; used only as a "has a face" flag. */
    @Column(name = "RekognitionFaceId")
    var latestFaceId: String? = null

    @OneToMany(mappedBy = "patient", cascade = [CascadeType.PERSIST])
    val visits: MutableList<Visit> = mutableListOf()

    companion object {
        fun formatDisplayId(villageCode: String?, id: Int): String =
            if (villageCode.isNullOrBlank()) "%04d".format(id) else "$villageCode${"%04d".format(id)}"

        fun formatFilterString(villageCode: String?, id: Int, contactNo: String?, name: String): String {
            val compact = if (villageCode.isNullOrBlank()) "$id" else "$villageCode$id"
            return "${formatDisplayId(villageCode, id)} $compact ${contactNo.orEmpty()} $name"
        }
    }
}
