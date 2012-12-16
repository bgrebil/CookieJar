using System;
using System.Linq;

namespace TestApp
{
   public partial class SetValue : System.Web.UI.Page
   {
      protected void Page_Load(object sender, EventArgs e)
      {
         if (Page.IsPostBack) {
            Session["test"] = this.TestValue.Value;
         }
         if (Session["test"] != null) {
            this.TestValue.Value = Session["test"].ToString();
         }
      }
   }
}