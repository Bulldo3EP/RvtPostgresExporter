namespace RvtPostgresExporter.Database.Secrets
{
    public interface IPasswordProvider
    {
        string GetPassword(string reference);
    }
}
