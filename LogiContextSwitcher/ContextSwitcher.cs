using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace LogiContextSwitcher
{
    public partial class ContextSwitcher : Form
    {
        public ContextSwitcher()
        {
            InitializeComponent();
        }

        private void ContextSwitcher_Load(object sender, EventArgs e)
        {

            label1.Text = Environment.UserName;
            label2.Text = string.Empty;
            button1.Text = @"Switch";
            var customerGroups = GetCustomerGroups();
            comboBox1.DataSource = customerGroups;
            comboBox1.DisplayMember = "GroupName";
            comboBox1.ValueMember = "GroupCode";

        }


        public class CustomerGroups
        {
            public string GroupName { get; set; }
            public string GroupCode { get; set; }
        }


        public List<CustomerGroups> GetCustomerGroups()
        {
            var logiService = new LogiAnalyticsService();

            var conn = logiService.GetConnString();

            using (SqlConnection connection = new SqlConnection(conn))
            {

                String sql = "SELECT GroupName, GroupCode FROM smsys.CustomerGroups order by GroupName asc";

                var customerGroups = new List<CustomerGroups>();

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            customerGroups.Add(new CustomerGroups(){ GroupName = reader.GetString(0), GroupCode = reader.GetString(1)} );
                        }
                    }
                }

                return customerGroups;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var selectedCustomerGroup = comboBox1.SelectedValue.ToString();

            label2.Text = $@"Switching to {selectedCustomerGroup}...";

            this.Enabled = false;
            try
            {
                var logiService = new LogiAnalyticsService();
                var result = logiService.SyncUserSessionAsync(Environment.UserName, selectedCustomerGroup?.ToUpper());

                label2.Text = result;
            }
            catch (Exception ex)
            {
                label2.Text = ex.Message;
            }
            finally
            {
                this.Enabled = true;
            }
        }
    }
}
