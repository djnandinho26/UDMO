using System;
using DigitalWorldOnline.Commons.Enums.Account;
using Microsoft.Data.SqlClient;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    internal class BanForCheating
    {
        // A string de conexão fixa dentro do arquivo
        private readonly string _connectionString = "Server=Elrayes\\SQLEXPRESS;Database=DMOX;User Id=sa;Password=sql@123;TrustServerCertificate=True";

        public void BanAccountForCheating(long accountId, AccountBlockEnum type, string reason, DateTime startDate, DateTime endDate)
        {
            var query = @"
                INSERT INTO Account.AccountBlock (AccountId, Type, Reason, StartDate, EndDate)
                VALUES (@AccountId, @Type, @Reason, @StartDate, @EndDate)";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@AccountId", accountId);
                        command.Parameters.AddWithValue("@Type", (int)type);
                        command.Parameters.AddWithValue("@Reason", reason);
                        command.Parameters.AddWithValue("@StartDate", startDate);
                        command.Parameters.AddWithValue("@EndDate", endDate);

                        command.ExecuteNonQuery();
                    }
                }

                Console.WriteLine("Banimento registrado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao registrar banimento: " + ex.Message);
            }
        }

        // Método simplificado para usar no código de banimento
        public string BanAccountWithMessage(long accountId, string Name, AccountBlockEnum type, string reason)
        {
            // Chama o método BanAccountForCheating
            /*
             Para banir por 1 hora a partir da data atual:   DateTime.Now.AddHours(1)
             Para banir por 1 dia a partir da data atual:    DateTime.Now.AddDays(1)
             Para banir por 1 semana a partir da data atual: DateTime.Now.AddDays(7)
             Para banir por 1 mês a partir da data atual:    DateTime.Now.AddMonths(1)
             Para banir por 1 ano a partir da data atual:    DateTime.Now.AddYears(1)
             Para banir permanentemente : DateTime.MaxValue

             fazer um sistema decrescente a cada ban
             
               DateTime endDate;

                // Definir a data de término do banimento com base no tipo de duração
                switch (durationType)
                {
                    case "1Hour":
                        endDate = DateTime.Now.AddHours(1);
                        break;
                    case "1Day":
                        endDate = DateTime.Now.AddDays(1);
                        break;
                    case "1Week":
                        endDate = DateTime.Now.AddDays(7);
                        break;
                    case "1Month":
                        endDate = DateTime.Now.AddMonths(1);
                        break;
                    case "1Year":
                        endDate = DateTime.Now.AddYears(1);
                        break;
                    default:
                        endDate = DateTime.Now;  // Se nenhum tipo for válido, banir por 0 dias (não recomendado)
                        break;
                }
             */
            BanAccountForCheating(accountId, type, reason, DateTime.Now, DateTime.MaxValue); // DateTime.Now.AddDays(1) = 1 day | DateTime.MaxValue = Permanent
            // Retorna a mensagem de banimento
            return $"User {Name} has been banned permanently for {reason}.";
        }
    }
}