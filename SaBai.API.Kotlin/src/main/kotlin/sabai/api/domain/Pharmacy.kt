package sabai.api.domain

import jakarta.persistence.Entity
import jakarta.persistence.EnumType
import jakarta.persistence.Enumerated
import jakarta.persistence.FetchType
import jakarta.persistence.GeneratedValue
import jakarta.persistence.GenerationType
import jakarta.persistence.Id
import jakarta.persistence.JoinColumn
import jakarta.persistence.ManyToOne
import jakarta.persistence.Table
import java.time.OffsetDateTime

@Entity
@Table(name = "MedicationActiveIngredients")
class MedicationActiveIngredient(
    var name: String,
    var unitOfMeasurement: String,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var fallBelow: Int = 0
    var remarks: String? = null
}

@Entity
@Table(name = "MedicationBrands")
class MedicationBrand(
    var name: String,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "ActiveIngredientId")
    var activeIngredient: MedicationActiveIngredient,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var remarks: String? = null
}

// "Order" is a JPQL keyword, so the entity gets another query name; the table is unchanged.
@Entity(name = "MedicationOrder")
@Table(name = "Orders")
class Order(
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "ConsultId")
    var consult: Consult,
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "MedicationBrandId")
    var medicationBrand: MedicationBrand,
    var dosageInstructions: String,
    var quantity: Int,
    var createdAt: OffsetDateTime,
) {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    var id: Int = 0

    var remarks: String? = null
    @Enumerated(EnumType.STRING) var status: OrderStatus = OrderStatus.Pending

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "ApprovedById")
    var approvedBy: StaffUser? = null

    var approvedAt: OffsetDateTime? = null
}
