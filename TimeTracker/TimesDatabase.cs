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
                var initSQL = SQLHolder.INITSQL  + "\n" + SQLHolder.ADITIONALSQL;

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
            command.CommandText = "DELETE FROM TIMES;"; 

            command.ExecuteNonQuery();

            connection.Close();
        }

        public void ClearDBUser()
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

            while( reader.Read())
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
        public int Cnum { get;  set; }
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
    }
}
