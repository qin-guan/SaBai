package sabai.api.domain

import jakarta.persistence.CascadeType
import jakarta.persistence.Column
import jakarta.persistence.Entity
import jakarta.persistence.FetchType
import jakarta.persistence.GeneratedValue
import jakarta.persistence.GenerationType
import jakarta.persistence.Id
import jakarta.persistence.JoinColumn
import jakarta.persistence.ManyToOne
import jakarta.persistence.OneToMany
import jakarta.persistence.OneToOne
import jakarta.persistence.PrePersist
import jakarta.persistence.Table
import java.math.BigDecimal
import java.time.OffsetDateTime
import java.time.ZoneOffset

@Entity
@Table(name = "Visits")
class Visit(
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "PatientId")
    var patient: Patient,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "VillageCodeId")
    var villageCode: VillageCode,
    var date: OffsetDateTime,
    var status: String = STARTED_STATUS,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    @OneToOne(mappedBy = "visit", cascade = [CascadeType.ALL])
    var vitals: Vitals? = null

    @OneToMany(mappedBy = "visit")
    val consults: MutableList<Consult> = mutableListOf()

    companion object {
        const val STARTED_STATUS = "started"
    }
}

@Entity
@Table(name = "Vitals")
class Vitals(
    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "VisitId")
    var visit: Visit,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    /** When the record was first saved. Null for records from before it was tracked. */
    var recordedAt: OffsetDateTime? = null

    @Column(precision = 5, scale = 2) var height: BigDecimal? = null
    @Column(precision = 5, scale = 2) var weight: BigDecimal? = null
    @Column(precision = 4, scale = 2) var temperature: BigDecimal? = null
    var systolic: Int? = null
    var diastolic: Int? = null
    var heartRate: Int? = null
    @Column(precision = 5, scale = 2) var hemocueCount: BigDecimal? = null
    var diabetesMellitus: Boolean? = null
    var urineTest: String? = null
    @Column(precision = 5, scale = 2) var bloodGlucoseNonFasting: BigDecimal? = null
    @Column(precision = 5, scale = 2) var bloodGlucoseFasting: BigDecimal? = null
    @Column(precision = 5, scale = 2) var hba1c: BigDecimal? = null
    var others: String? = null

    /** Replaces the SaveChanges override in AppDbContext: stamps the first save, whoever creates it. */
    @PrePersist
    fun stampRecordedAt() {
        if (recordedAt == null) recordedAt = OffsetDateTime.now(ZoneOffset.UTC)
    }
}
