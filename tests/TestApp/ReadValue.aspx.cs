using System;
using System.Linq;

namespace TestApp
{
   public partial class ReadValue : System.Web.UI.Page
   {
      protected void Page_Load(object sender, EventArgs e)
      {
         this.TestValue.InnerText = Session["test"] != null ? Session["test"].ToString() : "[Session Data Not Set]";
      }
   }
}