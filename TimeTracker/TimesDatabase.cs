using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;

namespace TimeTracker
{
    class TimesDatabase
    {
        private string dbFile = "Data.db";
        private int actualCustomerId = 1;
        private string actualName = "";

        public string GetActualName => actualName;

        public TimesDatabase()
        {
            dbFile = Path.Combine(Directory.GetCurrentDirectory(), dbFile);

            bool fileExists = File.Exists(dbFile);

            var connection = GetConnection();

            if (!fileExists)
            {
                var initSQL = SQLHolder.INITSQL + "\n" + SQLHolder.ADITIONALSQL + "\n" + SQLHolder.ADITIONALSQL2;

                var command = connection.CreateCommand();
                command.CommandText = initSQL;

                command.ExecuteNonQuery();
            }
            else
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'
                    AND name NOT LIKE 'sqlite_%';
                ";

                var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    int count = reader.GetInt32(0);

                    reader.Close();

                    if (count == 1)
                    {
                        var aditionalSQL = SQLHolder.ADITIONALSQL;
                        aditionalSQL += "\n" + SQLHolder.ADITIONALSQL2;
                        command.CommandText = aditionalSQL;
                        command.ExecuteNonQuery();

                    }
                    else if (count == 2)
                    {
                        var aditionalSQL = SQLHolder.ADITIONALSQL2;
                        command.CommandText = aditionalSQL;
                        command.ExecuteNonQuery();
                    }
                }
                else
                {
                    throw new Exception("DB INIT ERROR");
                }
            }

            connection.Close();

            var customers = GetCustomers();
            actualCustomerId = customers[0].ID;
            actualName = customers[0].Name;
        }
        

        public void WriteTime(DateTime start, DateTime end, TimeSpan timeWorked)
        {
            var connection = GetConnection();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO TIMES(start_date, end_date, time, cnum) VALUES(@ST_DT, @EN_DT, @TIME, @CNUM)";

            var param1 = new SqliteParameter("@ST_DT", start.ToString("yyyy.MM.dd HH:mm:ss"));
            var param2 = new SqliteParameter("@EN_DT", end.ToString("yyyy.MM.dd HH:mm:ss"));
            var param3 = new SqliteParameter("@TIME", timeWorked.Ticks);
            var param4 = new SqliteParameter("@CNUM", actualCustomerId);

            command.Parameters.Add(param1);
            command.Parameters.Add(param2);
            command.Parameters.Add(param3);
            command.Parameters.Add(param4);

            command.ExecuteNonQuery();

            connection.Close();
        }

        public void ClearDB()
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TIMES;" +
                "DELETE FROM ARCHIVE; DELETE FROM ARCHIVEINFO;";

            command.ExecuteNonQuery();

            connection.Close();
        }

        public void ClearTimeDBUser()
        {
            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TIMES WHERE cnum = @CNUM";

            var param1 = new SqliteParameter("@CNUM", $"{actualCustomerId}");

            command.Parameters.Add(param1);

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

            command.CommandText = "SELECT * FROM TIMES WHERE cnum = @CNUM";
            var param1 = new SqliteParameter("@CNUM", $"{actualCustomerId}");
            command.Parameters.Add(param1);

            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var ID = reader.GetInt32(0);
                var Start_timeText = reader.GetString(1);
                var End_timeText = reader.GetString(2);
                var timespanTICKS = reader.GetInt64(3);
                var cnum = reader.GetInt32(4);

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
                timeinfo.Cnum = cnum;

                list.Add(timeinfo);
            }

            reader.Close();
            conn.Close();

            return list.OrderBy(t => t.Start).ToList();
        }

        public List<CustomerInfo> GetCustomers()
        {
            var list = new List<CustomerInfo>();

            var connection = GetConnection();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM CUSTOMER;
            ";

            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var cnum = reader.GetInt32(0);
                var name = reader.GetString(1);

                list.Add(new(cnum, name));
            };

            reader.Close();

            connection.Close();

            return list;
        }

        public bool RenameActualCustomer(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            var connection = GetConnection();
            var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE CUSTOMER SET name = @NAME WHERE ID = @CNUM";

            var param1 = new SqliteParameter("@NAME", name);
            var param2 = new SqliteParameter("@CNUM", actualCustomerId);

            command.Parameters.Add(param1);
            command.Parameters.Add(param2);

            command.ExecuteNonQuery();

            connection.Close();

            actualName = name;

            return true;
        }

        public bool RemoveActualCustomer()
        {
            var customers = GetCustomers();

            if (customers.Count == 1)
                return false;

            var conn = GetConnection();

            var comm = conn.CreateCommand();
            comm.CommandText = @"
                DELETE FROM CUSTOMER WHERE ID = @CNUM;
                DELETE FROM TIMES WHERE CNUM = @CNUM;
                DELETE FROM ARCHIVE WHERE CNUM = @CNUM;
                DELETE FROM ARCHIVEINFO WHERE CNUM = @CNUM;
            ";

            var param1 = new SqliteParameter("@CNUM", actualCustomerId);
            comm.Parameters.Add(param1);

            comm.ExecuteNonQuery();

            conn.Close();

            var otherCustomer = customers.Where(c => c.ID != actualCustomerId).FirstOrDefault();
            actualCustomerId = otherCustomer!.ID;
            actualName = otherCustomer!.Name;

            return true;
        }

        public void DeleteActualArchive()
        {
            var conn = GetConnection();

            var comm = conn.CreateCommand();
            comm.CommandText = @"
                DELETE FROM ARCHIVE WHERE CNUM = @CNUM;
                DELETE FROM ARCHIVEINFO WHERE CNUM = @CNUM;
            ";

            var param1 = new SqliteParameter("@CNUM", actualCustomerId);
            comm.Parameters.Add(param1);
            comm.ExecuteNonQuery();

            conn.Close();
        }

        public bool AddCustomer(string name)
        {
            var customers = GetCustomers();
            if (customers.Any(c => c.Name == name)) return false;

            var conn = GetConnection();

            var comm = conn.CreateCommand();
            comm.CommandText = "INSERT INTO CUSTOMER(name) VALUES(@NAME)";
            var param1 = new SqliteParameter("@NAME", name);
            comm.Parameters.Add(param1);

            comm.ExecuteNonQuery();
            conn.Close();

            return true;
        }

        public string? ChangeCustomer(int ID)
        {
            var cust = GetCustomers();

            var selected = cust.FirstOrDefault(c => c.ID == ID);

            if (selected == null)
            {
                return null;
            }
            else
            {
                actualCustomerId = selected.ID;
                actualName = selected.Name;
                return selected.Name;
            }
        }

        private SqliteConnection GetConnection()
        {
            var connection = new SqliteConnection($"Data Source={dbFile}");
            connection.Open();

            return connection;
        }

        public void SaveToArchive(List<TimeInfo> data, string note)
        {
            if (data.Count <= 0) return;

            var maxDate = data.Max(d => d.Start);
            var minDate = data.Min(d => d.Start);

            var conn = GetConnection();

            var command = conn.CreateCommand();
            command.CommandText = "SELECT MAX(ID) FROM ARCHIVEINFO";

            var reader = command.ExecuteReader();

            int newArchiveId = 0;

            if (reader.Read()) 
            {
                newArchiveId = reader.IsDBNull(0) ? 1 : reader.GetInt32(0) + 1;
            }

            reader.Close();

            command.CommandText = "INSERT INTO ARCHIVEINFO(ID, cnum, note, start_date, end_date) " +
                "VALUES(@ID, @CNUM, @NOTE, @START, @END)";

            var parame1 = new SqliteParameter("@ID", newArchiveId);
            var parame2 = new SqliteParameter("@CNUM", actualCustomerId);
            var parame3 = new SqliteParameter("@NOTE", note);
            var parame4 = new SqliteParameter("@START", minDate.ToString("dd.MM.yyyy"));
            var parame5 = new SqliteParameter("@END", maxDate.ToString("dd.MM.yyyy"));

            command.Parameters.Add(parame1);
            command.Parameters.Add(parame2);
            command.Parameters.Add(parame3);
            command.Parameters.Add(parame4);
            command.Parameters.Add(parame5);

            command.ExecuteNonQuery();

            foreach (var timeInfo in data)
            {
                command.CommandText = "INSERT INTO ARCHIVE(ARCHIVE_ID, cnum, start_date, end_date, time) " +
                    "VALUES(@ARCHIVE_ID, @CNUM, @START, @END, @TIME)";
                var param1 = new SqliteParameter("@ARCHIVE_ID", newArchiveId);
                var param2 = new SqliteParameter("@CNUM", actualCustomerId);
                var param3 = new SqliteParameter("@START", timeInfo.Start.ToString("dd.MM.yyyy HH:mm:ss"));
                var param4 = new SqliteParameter("@END", timeInfo.End.ToString("dd.MM.yyyy HH:mm:ss"));
                var param5 = new SqliteParameter("@TIME", timeInfo.TimeWorked.TotalSeconds);
                command.Parameters.Clear();
                command.Parameters.Add(param1);
                command.Parameters.Add(param2);
                command.Parameters.Add(param3);
                command.Parameters.Add(param4);
                command.Parameters.Add(param5);
                command.ExecuteNonQuery();
            }

            conn.Close();
        }

        public List<ArchiveInfo> GetArchiveInfos()
        {
            List<int> ids = [];

            var conn = GetConnection();

            var command = conn.CreateCommand();
            command.CommandText = "SELECT ID FROM ARCHIVEINFO";

            var reader = command.ExecuteReader();
            
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }

            reader.Close();
            conn.Close();

            var archiveInfos = new List<ArchiveInfo>();

            foreach (var archiveId in ids)
            {
                var archiveInfo = GetArchiveInfoByID(archiveId);
                if (archiveInfo != null)
                {
                    archiveInfos.Add(archiveInfo);
                }
            }

            return archiveInfos;
        }

        public ArchiveInfo? GetArchiveInfoByID(int id)
        {
            var conn = GetConnection();

            var command = conn.CreateCommand();
            command.CommandText = "SELECT * FROM ARCHIVEINFO WHERE ID = @ID";

            var param = new SqliteParameter("@ID", id);
            command.Parameters.Add(param);

            var reader = command.ExecuteReader();

            if (reader.Read())
            {
                var archiveInfo = new ArchiveInfo
                {
                    ID = reader.GetInt32(0),
                    Start = DateTime.ParseExact(reader.GetString(3), "dd.MM.yyyy", CultureInfo.InvariantCulture),
                    End = DateTime.ParseExact(reader.GetString(4), "dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Note = reader.GetString(2)
                };

                reader.Close();

                command.CommandText = "SELECT * FROM ARCHIVE WHERE ARCHIVE_ID = @ID";

                var reader2 = command.ExecuteReader();
                while (reader2.Read())
                {
                    var TimeInfo = new TimeInfo
                    {
                        ID = reader2.GetInt32(0),
                        Start = DateTime.ParseExact(reader2.GetString(2), "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                        End = DateTime.ParseExact(reader2.GetString(3), "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                        TimeWorked = TimeSpan.FromSeconds(reader2.GetDouble(4)),
                        Cnum = reader2.GetInt32(1)
                    };

                    archiveInfo.timeInfos.Add(TimeInfo);
                }

                reader2.Close();
                conn.Close();
                return archiveInfo;
            }
            else
            {
                reader.Close();
                conn.Close();
                return null;
            }
        }

        public void DeleteArchiveInfo(int id)
        {
            var conn = GetConnection();
            var command = conn.CreateCommand();
            command.CommandText = "DELETE FROM ARCHIVEINFO WHERE ID = @ID; DELETE FROM ARCHIVE WHERE ARCHIVE_ID = @ID";
            var param = new SqliteParameter("@ID", id);
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            conn.Close();
        }

        public void DeleteArchiveOfUser() {
            var conn = GetConnection();
            var command = conn.CreateCommand();
            command.CommandText = "DELETE FROM ARCHIVEINFO WHERE CNUM = @CNUM; DELETE FROM ARCHIVE WHERE CNUM = @CNUM";
            var param = new SqliteParameter("@CNUM", actualCustomerId);
            command.Parameters.Add(param);
            command.ExecuteNonQuery();
            conn.Close();
        }
}

public class CustomerInfo(int ID, string name)
{
    public int ID { get; private set; } = ID;
    public string Name { get; set; } = name;
}

public class TimeInfo
{
    public int ID { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public TimeSpan TimeWorked { get; set; }
    public int Cnum { get; set; }
}

public class ArchiveInfo
{
    public int ID { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Note { get; set; } = string.Empty;
    public List<TimeInfo> timeInfos { get; set; } = [];

}

public class SQLHolder
{
    public readonly static string INITSQL = @"
        Create Table TIMES 
        (
	        ID INTEGER PRIMARY KEY AUTOINCREMENT,
	        start_date TEXT,
	        end_date TEXT,
	        time INTEGER
        );
    ";

    public readonly static string ADITIONALSQL = @"
        ALTER TABLE TIMES ADD COLUMN cnum INTEGER;
        Create Table CUSTOMER 
        (
	        ID INTEGER PRIMARY KEY AUTOINCREMENT,
	        name TEXT
        );

        UPDATE TIMES SET cnum = 1;

        INSERT INTO CUSTOMER(name) VALUES('DEFAULT');
    ";

    public readonly static string ADITIONALSQL2 = @"
        Create Table ARCHIVE 
        (
	        ARCHIVE_ID INTEGER,
            cnum INTEGER,
	        start_date TEXT,
	        end_date TEXT,
	        time INTEGER
        );

        CREATE TABLE ARCHIVEINFO
        (
            ID INTEGER,
            cnum INTEGER,
            note TEXT,
	        start_date TEXT,
	        end_date TEXT
        );
    ";
}}
