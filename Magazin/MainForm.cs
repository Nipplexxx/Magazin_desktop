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
    public partial class MainForm : Form
    {
        private readonly string connectionString = "Data Source=ACER;Initial Catalog=Shop;Integrated Security=True;Encrypt=False";
        private string currentTableName;
        private int currentPage = 1;
        private int totalPages = 1;
        private const int recordsPerPage = 10;

        private DataGridView dataGridView;
        private FlowLayoutPanel navigationPanel;
        private ComboBox tableSelector;
        private Button btnPrevious, btnNext, btnAdd, btnEdit, btnDelete;
        private Label lblPageInfo;

        public MainForm()
        {
            SetupUI();
            LoadAvailableTables();
        }

        private void SetupUI()
        {
            // ComboBox
            tableSelector = new ComboBox { Dock = DockStyle.Top };
            tableSelector.SelectedIndexChanged += TableSelector_SelectedIndexChanged;

            // DataGridView
            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            // Navigation Panel
            navigationPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            btnPrevious = new Button { Text = "Previous" };
            btnNext = new Button { Text = "Next" };
            btnAdd = new Button { Text = "Add Record" };
            btnEdit = new Button { Text = "Edit Record" };
            btnDelete = new Button { Text = "Delete Record" };
            lblPageInfo = new Label { AutoSize = true };

            btnPrevious.Click += BtnPrevious_Click;
            btnNext.Click += BtnNext_Click;
            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnDelete.Click += BtnDelete_Click;

            navigationPanel.Controls.AddRange(new Control[] { btnPrevious, lblPageInfo, btnNext, btnAdd, btnEdit, btnDelete });

            Controls.Add(dataGridView);
            Controls.Add(navigationPanel);
            Controls.Add(tableSelector);
        }

        private void LoadAvailableTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    DataTable tables = connection.GetSchema("Tables");
                    tableSelector.Items.AddRange(tables.Rows.Cast<DataRow>().Select(row => row["TABLE_NAME"].ToString()).ToArray());

                    if (tableSelector.Items.Count > 0)
                        tableSelector.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading tables: " + ex.Message);
                }
            }
        }

        private void TableSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tableSelector.SelectedItem != null)
            {
                currentTableName = tableSelector.SelectedItem.ToString();
                currentPage = 1;
                LoadTableData(currentTableName);
            }
        }

        private void LoadTableData(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return;

            string query = $@"
                SELECT * FROM [{tableName}]
                ORDER BY 1
                OFFSET {(currentPage - 1) * recordsPerPage} ROWS FETCH NEXT {recordsPerPage} ROWS ONLY";
            string countQuery = $"SELECT COUNT(*) FROM [{tableName}]";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();

                    // Получение общего количества записей
                    SqlCommand countCommand = new SqlCommand(countQuery, connection);
                    int totalRecords = (int)countCommand.ExecuteScalar();
                    totalPages = (int)Math.Ceiling(totalRecords / (double)recordsPerPage);

                    // Получение данных для текущей страницы
                    SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                    DataTable dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    dataGridView.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading data: " + ex.Message);
                }
            }

            UpdatePaginationControls();
        }

        private void UpdatePaginationControls()
        {
            btnPrevious.Enabled = currentPage > 1;
            btnNext.Enabled = currentPage < totalPages;
            lblPageInfo.Text = $"Page {currentPage} of {totalPages}";
        }

        private void BtnPrevious_Click(object sender, EventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                LoadTableData(currentTableName);
            }
        }

        private void BtnNext_Click(object sender, EventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                LoadTableData(currentTableName);
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(currentTableName))
            {
                var form = new RecordEditorForm(currentTableName, connectionString, null);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    LoadTableData(currentTableName);
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (dataGridView.SelectedRows.Count > 0 && !string.IsNullOrEmpty(currentTableName))
            {
                var selectedRow = ((DataRowView)dataGridView.SelectedRows[0].DataBoundItem).Row;
                var form = new RecordEditorForm(currentTableName, connectionString, selectedRow);
                if (form.ShowDialog() == DialogResult.OK)
                {
                    LoadTableData(currentTableName);
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView.SelectedRows.Count > 0 && !string.IsNullOrEmpty(currentTableName))
            {
                var selectedRow = dataGridView.SelectedRows[0];
                string primaryKeyColumn = dataGridView.Columns[0].Name;
                object primaryKeyValue = selectedRow.Cells[0].Value;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        string query = $"DELETE FROM [{currentTableName}] WHERE [{primaryKeyColumn}] = @Value";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@Value", primaryKeyValue);
                        command.ExecuteNonQuery();
                        LoadTableData(currentTableName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error deleting record: " + ex.Message);
                    }
                }
            }
        }
    }
}