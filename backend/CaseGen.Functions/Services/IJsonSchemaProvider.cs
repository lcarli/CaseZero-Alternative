namespace CaseGen.Functions.Services;

public interface IJsonSchemaProvider
{
    /// <summary>
    /// Retorna o conteúdo do schema (JSON) por nome.
    /// Aceita "DocumentAndMediaSpecs" ou "DocumentAndMediaSpecs.schema.json".
    /// </summary>
    string GetSchema(string name);
}
