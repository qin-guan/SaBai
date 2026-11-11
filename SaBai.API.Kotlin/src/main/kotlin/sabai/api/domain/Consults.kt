package sabai.api.domain

import jakarta.persistence.CascadeType
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
import jakarta.persistence.OrderBy
import jakarta.persistence.Table
import java.time.Duration
import java.time.OffsetDateTime

@Entity
@Table(name = "Consults")
class Consult(
    var date: OffsetDateTime,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "DoctorId")
    var doctor: StaffUser,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "VisitId")
    var visit: Visit,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var pastMedicalHistory: String? = null
    var consultation: String? = null
    var treatmentPlan: String? = null
    var referredFor: String? = null
    var referralNotes: String? = null
    var remarks: String? = null

    @OneToMany(mappedBy = "consult", cascade = [CascadeType.ALL], orphanRemoval = true)
    @OrderBy("id")
    val diagnoses: MutableList<Diagnosis> = mutableListOf()

    @OneToMany(mappedBy = "consult")
    val referrals: MutableList<Referral> = mutableListOf()

    @OneToMany(mappedBy = "consult", cascade = [CascadeType.PERSIST])
    val orders: MutableList<Order> = mutableListOf()

    fun isEditLocked(now: OffsetDateTime): Boolean = Duration.between(date, now) > EDIT_WINDOW

    companion object {
        val EDIT_WINDOW: Duration = Duration.ofHours(24)
    }
}

@Entity
@Table(name = "Diagnoses")
class Diagnosis(
    var details: String,
    @Enumerated(EnumType.STRING) var category: DiagnosisCategory?,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "ConsultId")
    var consult: Consult,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0
}

@Entity
@Table(name = "Referrals")
class Referral(
    var referredFor: String,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "ConsultId")
    var consult: Consult,
    var createdAt: OffsetDateTime,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var referralNotes: String? = null
    @Enumerated(EnumType.STRING) var referralState: ReferralState = ReferralState.New
    var referralOutcome: String? = null
}
