using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TimeTracker
{
    class TimesDatabase
    {
        private string dbFile = "DB/Data.db";

        public TimesDatabase()
        {
            dbFile = Path.Combine(Directory.GetCurrentDirectory(), dbFile);

            if (!File.Exists(dbFile))
            {

                var connection = GetConnection();

                var initSQL = File.ReadAllText("DB/dbinit.sql");

                var command = connection.CreateCommand();
                command.CommandText = initSQL;

                command.ExecuteNonQuery();
                connection.Close();
            }
        }

        public void WriteTime(DateTime start, DateTime end, TimeSpan timeWorked)
        {
            var connection = GetConnection();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO TIMES(start_date, end_date, time) VALUES(@ST_DT, @EN_DT, @TIME)";

            var param1 = new SqliteParameter("@ST_DT", start.ToString("yyyy.MM.dd HH:mm:ss"));
            var param2 = new SqliteParameter("@EN_DT", end.ToString("yyyy.MM.dd HH:mm:ss"));
            var param3 = new SqliteParameter("@TIME", timeWorked.Ticks);

            command.Parameters.Add(param1);
            command.Parameters.Add(param2);
            command.Parameters.Add(param3);

            command.ExecuteNonQuery();

            connection.Close();
        } 

        public void ClearDB()
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TIMES"; 

            command.ExecuteNonQuery();

            connection.Close();
        }

        public List<TimeInfo> GetInTime(DateTime start, DateTime end)
        {
            return GetAll().Where(t => t.Start >= start && t.End <= end).ToList();
        }

        public void Delete(int ID)
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TIMES WHERE ID = @id";

            var param = new SqliteParameter("@id", ID);

            command.Parameters.Add(param);

            command.ExecuteNonQuery();

            connection.Close();
        }

        public List<TimeInfo> GetAll()
        {
            List<TimeInfo> list = [];

            var conn = GetConnection();
            var command = conn.CreateCommand();

            command.CommandText = "SELECT * FROM TIMES";

            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var ID = reader.GetInt32(0);
                var Start_timeText = reader.GetString(1);
                var End_timeText = reader.GetString(2);
                var timespanTICKS = reader.GetInt64(3);

                var timeinfo = new TimeInfo();
                timeinfo.ID = ID;
                timeinfo.Start = DateTime.ParseExact(
                    Start_timeText,
                    "yyyy.MM.dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                ); 

                timeinfo.End = DateTime.ParseExact(
                    End_timeText,
                    "yyyy.MM.dd HH:mm:ss",
                    CultureInfo.InvariantCulture
                );

                timeinfo.TimeWorked = new TimeSpan(timespanTICKS);

                list.Add(timeinfo);
            }

            reader.Close();
            conn.Close();

            return list;
        }

        private SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection($"Data Source={dbFile}"); 
            connection.Open();

            return connection;
        }
        
    }

    public class TimeInfo
    {
        public int ID { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan TimeWorked { get; set; }
    }
}
