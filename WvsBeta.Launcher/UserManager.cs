using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySqlConnector;
using WvsBeta.Database;

namespace WvsBeta.Launcher
{
    public partial class UserManager : Form
    {
        private MySQL_Connection _connection { get; }

        private BindingList<User> users { get; }

        public UserManager(MySQL_Connection connection)
        {
            InitializeComponent();
            _connection = connection;
            users = new BindingList<User>();
        }

        class User : INotifyPropertyChanged
        {
            public int? ID { get; private set; } = null;
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public DateTime BanExpire { get; set; } = new DateTime(2000, 1, 1);
            public byte GMLevel { get; set; } = 0;
            public int CharDeletePassword { get; set; } = 11111111;

            public User(int id)
            {
                ID = id;
            }

            public User()
            {
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public void Save(MySQL_Connection connection)
            {
                var arguments = new object[]
                {
                    "@id", ID,
                    "@username", Username,
                    "@password", Password,
                    "@banExpire", BanExpire,
                    "@admin", GMLevel,
                    "@charDeletePassword", CharDeletePassword
                };

                if (ID == null)
                {
                    connection.RunQuery(
                        """
                        INSERT INTO users
                        (username, password, ban_expire, admin, email, char_delete_password)
                        VALUES
                        (@username, @password, @banExpire, @admin, '', @charDeletePassword)
                        """,
                        arguments
                    );
                    ID = connection.GetLastInsertId();
                }
                else
                {
                    connection.RunQuery(
                        """
                        UPDATE users
                        SET
                        username = @username,
                        password = @password,
                        ban_expire = @banExpire,
                        admin = @admin,
                        char_delete_password = @charDeletePassword
                        WHERE ID = @id
                        """,
                        arguments
                    );
                }
            }

            public void Delete(MySQL_Connection connection)
            {
                if (ID == null) return;

                connection.RunQuery(
                    """
                    DELETE FROM users
                    WHERE ID = @id
                    """,
                    "@id", ID
                );
            }
        }

        private void UserManager_Load(object sender, EventArgs e)
        {
            using var reader = (MySqlDataReader)_connection.RunQuery("SELECT * FROM users ORDER BY ID ASC");
            while (reader.Read())
            {
                users.Add(new User(reader.GetInt32("ID"))
                {
                    Username = reader.GetString("username"),
                    Password = reader.GetString("password"),
                    BanExpire = reader.GetDateTime("ban_expire"),
                    GMLevel = reader.GetByte("admin"),
                    CharDeletePassword = reader.GetInt32("char_delete_password"),
                });
            }

            dgvUsers.DataSource = users;
        }

        private void SaveCurrentRow(int row)
        {
            try
            {
                var rowElem = dgvUsers.Rows[row];
                (rowElem.DataBoundItem as User)?.Save(_connection);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save the changes: {ex}");
            }
        }

        private void dgvUsers_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            SaveCurrentRow(e.RowIndex);
        }

        private void dgvUsers_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            SaveCurrentRow(e.RowIndex);
        }

        private void addToolStripMenuItem_Click(object sender, EventArgs e)
        {
            users.Add(new User());
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (dgvUsers.CurrentRow == null) return;

            var user = dgvUsers.CurrentRow.DataBoundItem as User;

            if (user == null) return;

            if (MessageBox.Show($"Are you sure you want to delete {user.Username}?", "Hol' up", MessageBoxButtons.OKCancel) != DialogResult.OK)
            {
                return;
            }

            user.Delete(_connection);

            users.Remove(user);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dgvUsers.EndEdit();
        }

        private void dgvUsers_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SaveCurrentRow(e.RowIndex);
        }
    }
}