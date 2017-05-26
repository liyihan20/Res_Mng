using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using org.in2bits.MyXls;

namespace RestaurantMng.Controllers
{
    public class ExcelController : BaseController
    {

        public void ExportFinishedOrderExcel(string searchValue, string fromDate, string toDate)
        {
            DateTime fromDateDT = DateTime.Parse(fromDate);
            DateTime toDateDT = DateTime.Parse(toDate).AddDays(1);

            var myData = (from o in db.dn_order
                          where (o.status == "用户取消"
                          || o.status == "已完成")
                          && (o.user_name.Contains(searchValue)
                          || o.order_no.Contains(searchValue))
                          && o.arrive_day >= fromDateDT
                          && o.arrive_day <= toDateDT
                          orderby o.arrive_day
                          select o).ToList();

            //列宽：
            ushort[] colWidth = new ushort[] {14,16,14,20,14,20,12,12,12,14,
                                             14,12,20,14,14,32,14,16,32,12,
                                             24,14,20,12,20,12,14,14};

            //列名：
            string[] colName = new string[] {"状态","订单编号","姓名","到达时间","到达时段","申请时间","人数","是否包间","是否配送","台桌/房间号",
                                            "预约牌号","支付方式","支付时间","应收金额","实收金额","配送地址","收件人","联系电话","用户备注","审核结果",
                                            "审核意见","审核人","审核时间","菜式编号","菜式名","数量","单价","小计"};

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = string.Format("会所预约申请_{0}.xls", DateTime.Now.ToShortDateString());
            Worksheet sheet = xls.Workbook.Worksheets.Add("信息列表");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;

            //设置列宽
            ColumnInfo col;
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet.AddColumnInfo(col);
            }

            Cells cells = sheet.Cells;
            int rowIndex = 1;
            int colIndex = 1;

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            //"状态","订单编号","姓名","到达时间","到达时段","申请时间","人数","是否包间","是否配送","台桌/房间号",
            //"预约牌号","支付方式","支付时间","应收金额","实收金额","配送地址","收件人","联系电话","用户备注","审核结果",
            //"审核意见","审核人","审核时间","菜式编号","菜式名","数量","单价","小计"
            rowIndex = 2;
            foreach (var d in myData) {
                colIndex = 1;
                cells.Add(rowIndex, colIndex, d.status);
                cells.Add(rowIndex, ++colIndex, d.order_no);
                cells.Add(rowIndex, ++colIndex, d.user_name);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.arrive_day).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.arrive_time);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.create_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.people_num);
                cells.Add(rowIndex, ++colIndex, d.is_in_room==true?"是":"否");
                cells.Add(rowIndex, ++colIndex, d.is_delivery==true?"是":"否");
                cells.Add(rowIndex, ++colIndex, d.desk_num);

                cells.Add(rowIndex, ++colIndex, d.book_card_num);
                cells.Add(rowIndex, ++colIndex, d.payment_type);
                cells.Add(rowIndex, ++colIndex, d.payment_time == null ? "" : ((DateTime)d.payment_time).ToString("yyyy-MM-dd HH:mm"));
                cells.Add(rowIndex, ++colIndex, d.total_price);
                cells.Add(rowIndex, ++colIndex, d.real_price);
                cells.Add(rowIndex, ++colIndex, d.delivery_addr);
                cells.Add(rowIndex, ++colIndex, d.recipient);
                cells.Add(rowIndex, ++colIndex, d.recipient_phone);
                cells.Add(rowIndex, ++colIndex, d.user_comment);
                cells.Add(rowIndex, ++colIndex, d.audit_result==true?"OK":d.audit_result==false?"NG":"");

                cells.Add(rowIndex, ++colIndex, d.audit_comment);
                cells.Add(rowIndex, ++colIndex, d.auditor_name);
                cells.Add(rowIndex, ++colIndex, d.audit_time == null ? "" : ((DateTime)d.audit_time).ToString("yyyy-MM-dd HH:mm"));
                foreach (var entry in d.dn_orderEntry) {
                    var tmpColIndex = colIndex;
                    cells.Add(rowIndex, ++colIndex, entry.dn_dishes.number);
                    cells.Add(rowIndex, ++colIndex, entry.dn_dishes.name);
                    cells.Add(rowIndex, ++colIndex, entry.qty);
                    cells.Add(rowIndex, ++colIndex, entry.price);
                    cells.Add(rowIndex, ++colIndex, entry.qty * entry.price);
                    rowIndex++;
                    colIndex = tmpColIndex;
                }
            }
            xls.Send();
        }
    }
}
