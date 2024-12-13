using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Magazin
{
    public partial class RecordEditorForm : Form
    {
        private readonly string tableName;
        private readonly string connectionString;
        private readonly DataRow existingRow;
        private DataTable tableSchema;

        public RecordEditorForm(string tableName, string connectionString, DataRow existingRow = null)
        {
            InitializeComponent();
            this.tableName = tableName;
            this.connectionString = connectionString;
            this.existingRow = existingRow;

            LoadSchema();
            InitializeFormFields();
        }

        private void LoadSchema()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    SqlCommand command = new SqlCommand($"SELECT TOP 0 * FROM {tableName}", connection);
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataTable schemaTable = new DataTable();
                    adapter.FillSchema(schemaTable, SchemaType.Source);
                    tableSchema = schemaTable;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading table schema: " + ex.Message);
                    Close();
                }
            }
        }

        private void InitializeFormFields()
        {
            if (tableSchema == null) return;

            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            foreach (DataColumn column in tableSchema.Columns)
            {
                if (column.AutoIncrement) continue; // Пропустить автоинкрементные столбцы

                Label label = new Label { Text = column.ColumnName, Dock = DockStyle.Fill };
                TextBox textBox = new TextBox { Name = column.ColumnName, Dock = DockStyle.Fill };

                if (existingRow != null && existingRow.Table.Columns.Contains(column.ColumnName))
                    textBox.Text = existingRow[column.ColumnName]?.ToString();

                layout.Controls.Add(label);
                layout.Controls.Add(textBox);
            }

            Button btnSave = new Button { Text = "Save", Dock = DockStyle.Bottom };
            btnSave.Click += BtnSave_Click;

            Controls.Add(layout);
            Controls.Add(btnSave);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    SqlCommand command;

                    if (existingRow == null)
                    {
                        // Добавление новой записи
                        string columns = string.Join(", ", tableSchema.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement).Select(c => c.ColumnName));
                        string values = string.Join(", ", tableSchema.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement).Select(c => "@" + c.ColumnName));
                        string query = $"INSERT INTO {tableName} ({columns}) VALUES ({values})";

                        command = new SqlCommand(query, connection);
                        foreach (DataColumn column in tableSchema.Columns)
                        {
                            if (!column.AutoIncrement)
                                command.Parameters.AddWithValue("@" + column.ColumnName, GetControlValue(column.ColumnName));
                        }
                    }
                    else
                    {
                        // Обновление существующей записи
                        string setClause = string.Join(", ", tableSchema.Columns.Cast<DataColumn>().Where(c => !c.AutoIncrement).Select(c => $"{c.ColumnName} = @{c.ColumnName}"));
                        string primaryKeyColumn = tableSchema.PrimaryKey.FirstOrDefault()?.ColumnName;

                        if (string.IsNullOrEmpty(primaryKeyColumn))
                            throw new Exception("Primary key not found.");

                        string query = $"UPDATE {tableName} SET {setClause} WHERE {primaryKeyColumn} = @{primaryKeyColumn}";

                        command = new SqlCommand(query, connection);
                        foreach (DataColumn column in tableSchema.Columns)
                        {
                            command.Parameters.AddWithValue("@" + column.ColumnName, GetControlValue(column.ColumnName));
                        }
                    }

                    command.ExecuteNonQuery();
                    DialogResult = DialogResult.OK;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving record: " + ex.Message);
            }
        }

        private object GetControlValue(string columnName)
        {
            var control = Controls.Find(columnName, true).FirstOrDefault();
            return control is TextBox textBox ? (object)textBox.Text : DBNull.Value;
        }
    }
}
