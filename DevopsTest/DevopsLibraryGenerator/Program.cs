// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using System.Text;

Console.WriteLine("Hello, World!");

var text = File.ReadAllText("devopsApi.json");
var classTemplateText = File.ReadAllText("ClassTemplate.txt");
var propertyTemplateText = File.ReadAllText("PropertyTemplate.txt");

var jObject = JsonConvert.DeserializeObject<dynamic>(text);

Directory.CreateDirectory("Models");

foreach (var model in jObject.Models)
{
    string finalClassText;
    

    if (DetectEnum(model.Name))
    {
        var propertiesTextSB = new StringBuilder();
        foreach (var property in model.Properties)
        {
            propertiesTextSB.Append(
                ConvertToEnum(
                    property.Name.ToString(),
                    property.Description.ToString()));
            propertiesTextSB.AppendLine(",");
        }

        finalClassText = string.Format(classTemplateText,
            "NamespaceRoot",
            model.Description,
            "enum",
            model.Name,
            propertiesTextSB.ToString());
    }
    else
    {
        var propertiesTextSB = new StringBuilder();
        foreach (var property in model.Properties)
        {
            propertiesTextSB.Append(string.Format(propertyTemplateText,
                property.Description.ToString(),
                GenerateProperties(property.Name, property.Description),
                CorrectTypeName(property.Type.ToString()),
                ReplaceKeyword(property.Name.ToString())));
        }

        finalClassText = string.Format(classTemplateText,
            "NamespaceRoot",
            model.Description,
            "class",
            model.Name,
            propertiesTextSB.ToString());
    }
    File.WriteAllText($"Models/{model.Name}.cs", finalClassText);    
}

string ReplaceKeyword(string name)
{
    return name switch
    {
        "string" => "@string",
        "abstract" => "@abstract",
        "fixed" => "@fixed",
        "event" => "@event",
        "double" => "@double",
        "object" => "@object",
        "$top" => "top",
        "$skip" => "skip",
        "$orderBy" => "orderBy",
        "$expand" => "expand",
        "new" => "@new",
        "private" => "@private",
        "implicit" => "@implicit",
        "class" => "@class",
        "namespace" => "@namespace",
        "operator" => "@operator",
        "internal" => "@internal",
        "continue" => "@continue",
        "public" => "@public",
        "ref" => "@ref",
        "sealed" => "@sealed",
        "lock" => "@lock",
        _ => name
    };
}

string CorrectTypeName(string typeName)
{
    typeName = typeName
        .Replace("integer", "int")
        .Replace("boolean", "bool")
        .Replace("number", "long");

    if (typeName.Contains("&lt;"))
    {
        typeName = typeName
            .Replace("&lt;", "Dictionary<")
            .Replace("&nbsp;", " ")
            .Replace("&gt;", ">");
    }

    typeName = GeneraliseUndefinedTypes(typeName);

    return string.IsNullOrWhiteSpace(typeName) ? "object" : typeName;
}

string GeneraliseUndefinedTypes(string typeName)
{
    return typeName switch
    {
        // Help in generating these is welcome.
        "array[]" or 
        "IDomainId" or 
        "ArtifactProperties" or 
        "ChangeCountDictionary" or 
        "BatchOperationData" or 
        "WorkingDays[]" or 
        "VariableGroupProviderData" or 
        "SupportedScopes[]" => "dynamic",

        _ => typeName,
    };
    ;
}

string GenerateProperties(string name, string description)
{
    string returnString = "";
    if (name.Contains("$"))
    {
        returnString += $"\r\n    [System.Text.Json.Serialization.JsonPropertyName(\"{name}\")]\r\n    [Newtonsoft.Json.JsonProperty(\"{name}\")]";
    }
    if (description is not null && description.Contains("Deprecated")) 
    {
        returnString += $"\r\n    [Obsolete(\"{description}\")]";
    }

    return returnString;
}

bool DetectEnum(string name)
{
    return
       name.EndsWith("Type")
       || name.EndsWith("State");

}

string ConvertToEnum(string enumValue, string enumDescription)
{
    var returnString = "";
    if (!string.IsNullOrWhiteSpace(enumDescription))
    {
        returnString += $"    /// <summary>\r\n    /// {enumDescription}\r\n    /// </summary>\r\n";
    }
    returnString += ReplaceKeyword(enumValue);
    return returnString;
}