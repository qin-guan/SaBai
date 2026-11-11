package sabai.api.web

import jakarta.validation.Constraint
import jakarta.validation.ConstraintValidator
import jakarta.validation.ConstraintValidatorContext
import jakarta.validation.Payload
import kotlin.reflect.KClass

/**
 * Null (left unchanged by a PATCH) or containing text: FluentValidation's
 * `.NotEmpty().When(x => x.Name is not null)`.
 */
@Target(AnnotationTarget.FIELD, AnnotationTarget.VALUE_PARAMETER)
@Retention(AnnotationRetention.RUNTIME)
@Constraint(validatedBy = [NullOrNotBlankValidator::class])
annotation class NullOrNotBlank(
    val message: String = "must not be blank",
    val groups: Array<KClass<*>> = [],
    val payload: Array<KClass<out Payload>> = [],
)

class NullOrNotBlankValidator : ConstraintValidator<NullOrNotBlank, String?> {
    override fun isValid(value: String?, context: ConstraintValidatorContext) = value == null || value.isNotBlank()
}

/** Trimmed text, or null when blank: the C# `Normalize` helper. */
fun String?.trimToNull(): String? = this?.trim()?.ifEmpty { null }
