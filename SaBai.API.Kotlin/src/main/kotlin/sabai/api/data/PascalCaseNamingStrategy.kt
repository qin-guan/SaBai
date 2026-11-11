package sabai.api.data

import org.hibernate.boot.model.naming.Identifier
import org.hibernate.boot.model.naming.PhysicalNamingStrategy
import org.hibernate.engine.jdbc.env.spi.JdbcEnvironment

/**
 * Maps Kotlin's camelCase properties to the quoted PascalCase names EF Core created
 * (`dateOfBirth` -> `"DateOfBirth"`), so no column needs an explicit @Column(name).
 */
class PascalCaseNamingStrategy : PhysicalNamingStrategy {
    override fun toPhysicalCatalogName(name: Identifier?, env: JdbcEnvironment) = name

    override fun toPhysicalSchemaName(name: Identifier?, env: JdbcEnvironment) = name

    override fun toPhysicalTableName(name: Identifier, env: JdbcEnvironment) = pascal(name)

    override fun toPhysicalSequenceName(name: Identifier, env: JdbcEnvironment) = pascal(name)

    override fun toPhysicalColumnName(name: Identifier, env: JdbcEnvironment) = pascal(name)

    private fun pascal(name: Identifier): Identifier =
        Identifier.toIdentifier(name.text.replaceFirstChar { it.uppercaseChar() }, true)
}
