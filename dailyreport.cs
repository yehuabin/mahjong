  public bool GenerateDailyReportRecord(System.DateTime startdate, System.DateTime enddate, string[] employeeIds)
        {
            var _transferbi = humanresource.business.ABusinessFactory.BusinessFactory.TransferBI;
            //var _holidaybi = att.business.ABOFactory.BOFactory.GetHolidayBI();
            //  var _attleavebillbi = att.business.ABOFactory.BOFactory.GetAttLeaveBillBI();
            var recordbo = new att.business.bo.DefaultAttRecordBO();
            var employeebi = humanresource.business.ABusinessFactory.BusinessFactory.EmployeeBI;
            var employeeshiftbo = new att.business.bo.EmployeeShiftBO();
            var deptshiftbi = new EtierHR.business.bo.DeptShiftBO();
            var detailbo = new att.business.bo.ShiftDetailBO();
            var tempbo = new TempShiftBOEx();

            if (enddate > DateTime.Today)
                enddate = DateTime.Today;

            if (startdate > enddate)
            {
                return false;
            }
            int days = (enddate - startdate).Days;
            string strStartdate, strEnddate;
            strStartdate = startdate.ToString("yyyy-MM-dd");
            strEnddate = enddate.ToString("yyyy-MM-dd");

            string strCondition = "employeeid  in (" + string.Join(",", employeeIds) + ")";

            DataView dvEmployee = employeebi.QueryAllByCondition(strCondition);
            DataView dvTransfer = _transferbi.QueryByCondition(strCondition + " and TransferDate>='" + strStartdate + "'");
            var allTransferList = Utility.TransferToList(dvTransfer);
            DataView dvCheckRecord = recordbo.QueryByCondition(strCondition + " and checkdate>='" + startdate.AddDays(-1).ToString("yyyy-MM-dd")
                + "' and checkdate<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") + "'");
            dvCheckRecord.Sort = "checkdate,checktime";

            DataView dvTempshift =
                tempbo.QueryByCondition(strCondition + " and startdate>='" + strStartdate
                                        + "' and startdate<='" + strEnddate + "'");

            var deptbi = humanresource.BusinessFactory.Create<humanresource.business.bi.DepartmentBI>();
            var allDept = deptbi.GetAllDepartments();
            //获取班次列表
            var custombi =
                humanresource.business.ABusinessFactory.BusinessFactory.CreateCustomTableBI(
                    humanresource.Models.CustomTable.Department);
            var dvShifts = custombi.QueryByCondition(string.Empty);
            DepartmentShiftModel deptShifts = new DepartmentShiftModel(allDept, dvShifts);

            var parentDeptBI = etier.business.DBBusinessFactory.BusinessFactory.CreateParentDeptsBI();
            var allDeptList = parentDeptBI.QueryByCondition(null);

            //员工排班列表(全部)
            var dvempshiftAll = employeeshiftbo.QueryByCondition(" startdate<='" + strEnddate + "' and enddate>='" + strStartdate + "'");

            //部门排班次列表            
            var dvdeptshift = deptshiftbi.GetList(null, null, null, startdate, enddate);

            var dvShiftDetail = detailbo.QueryByCondition("attclassname is not null");
            var access = DBAccessFactory.GetDBAccess();
            var dvEmployeeDailyReport = access.QueryDataView(string.Format(
                      "SELECT * from t_attdailyreport WHERE employeeid in ({0}) and reportdate>='{1}' and reportdate<='{2}'",
                       string.Join(",", employeeIds), strStartdate, strEnddate), "t_attdailyreport");
            foreach (DataRowView drvemp in dvEmployee)
            {
                string employeeId = drvemp["employeeid"].ToString();
                var _log_startDate = DateTime.Now; //仅供日志使用
                humanresource.BusinessLogger.BeginAction(startdate, enddate, employeeId); //记录日志

                string indate = null, outdate = string.Empty, attEndCheckDate = string.Empty;

                #region 获取员工入职日期和考勤截止日日期

                indate = drvemp["startdate"].ToString();
                outdate = drvemp["outdate"].ToString();

                //人事异动表
                //var transferList = _transferbi.GetTransferList(employeeId);
                //var indexTransfer =
                //    transferList.FindIndex(
                //        p =>
                //            p.TransferType == HumanResource.Util.TransferType.DISMISS  &&
                //            p.TransferDate >= startdate);
                //if (indexTransfer != -1)
                //{
                //    indate = strStartdate;
                //}

                #endregion

                try
                {

                  
                    #region 获取签卡记录列表、临时排班列表、员工班排列表、节假日列表、请假列表、员工异动表
                    System.Data.DataView dvResult = (new DSAttDailyReport()).t_attdailyreport.DefaultView;// access.QueryDataView("SELECT * FROM t_attdailyreport where 1=2", "t_attdailyreport");// (new DSAttDailyReport()).t_attdailyreport.DefaultView;
                    //string lastchecktime = this.GetLastCheckRecord(employeeId, startdate);

                    int curindex = 0;
                    //if (lastchecktime == null)
                    //{

                        dvCheckRecord.RowFilter = "employeeid='" + employeeId + "' and checkdate>='" +
                                                  startdate.AddDays(-1).ToString("yyyy-MM-dd") +
                                                  "' and checkdate<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") + "'";
                    //}
                    //else
                    //{
                    //    dvCheckRecord.RowFilter = "employeeid='" + employeeId + "' and checkdate+' '+checktime>'" +
                    //                              lastchecktime + "' and checkdate<='" +
                    //                              enddate.AddDays(1).ToString("yyyy-MM-dd") + "'";
                    //    //curindex = 1;
                    //}
                    //System.Data.DataView dvHoliday =
                    //    _holidaybi.QueryByCondition("endtime>='" + startdate.ToString("yyyy-MM-dd") +
                    //                                "' and starttime<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") +
                    //                                "'");
                    //System.Data.DataView dvLeavebill =
                    //    _attleavebillbi.QueryByCondition("employeeid='" + employeeId + "' and endtime>='" +
                    //                                     startdate.ToString("yyyy-MM-dd") + "' and starttime<='" +
                    //                                     enddate.AddDays(1).ToString("yyyy-MM-dd") +
                    //                                     "' and (needcheckout='是' or needcheckin='是')");
                    //System.Collections.ArrayList alLeaveCheckOut = new System.Collections.ArrayList();
                    //System.Collections.ArrayList alLeaveCheckIn = new System.Collections.ArrayList();

                    //清除小于打开间隔的打卡记录
                    if (dvCheckRecord.Count > 0)
                    {
                        if (this.AttParams.IntervalMinute > 0)
                        {
                            int i = 1;
                            DateTime time =
                                DateTime.Parse(dvCheckRecord[0][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() +
                                               " " +
                                               dvCheckRecord[0][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString());
                            while (i < dvCheckRecord.Count)
                            {
                                DateTime ctime =
                                    DateTime.Parse(
                                        dvCheckRecord[i][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " +
                                        dvCheckRecord[i][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString());
                                System.TimeSpan ts = new TimeSpan(0);
                                if ((ctime - time).TotalMinutes < this.AttParams.IntervalMinute)
                                {
                                    dvCheckRecord[i].Delete();
                                }
                                else
                                {
                                    time = ctime;
                                    i++;
                                }
                            }
                        }
                        else
                        {
                            int i = 1;
                            string time = dvCheckRecord[0][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " +
                                          dvCheckRecord[0][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                            while (i < dvCheckRecord.Count)
                            {
                                string ctime = dvCheckRecord[i][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() +
                                               " " +
                                               dvCheckRecord[i][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                if (time == ctime)
                                {
                                    dvCheckRecord[i].Delete();
                                }
                                else
                                {
                                    time = ctime;
                                    i++;
                                }
                            }
                        }
                    }

                    //取出要求打卡的请假
                    //if (dvLeavebill != null && dvLeavebill.Count > 0)
                    //{
                    //    dvLeavebill.Sort = "starttime";
                    //    for (int i = 0; i < dvLeavebill.Count; i++)
                    //    {
                    //        if (dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.NEEDCHECKOUT].Equals("是"))
                    //        {
                    //            //请假开始时间
                    //            DateTime starttime =
                    //                DateTime.Parse(
                    //                    dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.STARTTIME].ToString());

                    //            //请假开始签卡时间
                    //            DateTime checkstarttime =
                    //                starttime.AddMinutes(-this.AttParams.GetValueAsInt("请假允许提前多少分钟签出"));
                    //            //请假结束签卡时间
                    //            DateTime checkendtime = starttime.AddMinutes(this.AttParams.LeaveCheckoutLater);

                    //            dvAttRecord.RowFilter = "checkdate+' '+checktime>='" +
                    //                                    checkstarttime.ToString("yyyy-MM-dd HH:mm") +
                    //                                    "' and checkdate+' '+checktime<='" +
                    //                                    checkendtime.ToString("yyyy-MM-dd HH:mm") + "'";
                    //            if (dvAttRecord.Count > 0)
                    //            {
                    //                alLeaveCheckOut.Add(dvAttRecord[0]["checkdate"].ToString() + " " +
                    //                                    dvAttRecord[0]["checktime"].ToString());
                    //                dvAttRecord.Delete(0);
                    //            }
                    //        }
                    //        if (dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.NEEDCHECKIN].Equals("是"))
                    //        {
                    //            //请假结束时间
                    //            DateTime endtime =
                    //                DateTime.Parse(
                    //                    dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.ENDTIME].ToString());

                    //            //请假开始签卡时间
                    //            DateTime checkstarttime = endtime.AddMinutes(0 - this.AttParams.LeaveCheckinEarlier);
                    //            //请假结束签卡时间
                    //            DateTime checkendtime = endtime.AddMinutes(this.AttParams.GetValueAsInt("请假允许延后多少分钟签入"));

                    //            dvAttRecord.RowFilter = "checkdate+' '+checktime>='" +
                    //                                    checkstarttime.ToString("yyyy-MM-dd HH:mm") +
                    //                                    "' and checkdate+' '+checktime<='" +
                    //                                    checkendtime.ToString("yyyy-MM-dd HH:mm") + "'";
                    //            if (dvAttRecord.Count > 0)
                    //            {
                    //                alLeaveCheckIn.Add(dvAttRecord[dvAttRecord.Count - 1]["checkdate"].ToString() + " " +
                    //                                   dvAttRecord[dvAttRecord.Count - 1]["checktime"].ToString());
                    //                dvAttRecord.Delete(dvAttRecord.Count - 1);
                    //            }
                    //        }
                    //    }
                    //    dvAttRecord.RowFilter = null;
                    //}

                    #endregion

                    //bool bHasHoliday = (dvHoliday.Count > 0);
                    string shiftid = null;
                    DataView dvDetail = null;

                    var deptid = drvemp["deptid"].ToString();
                    var transferList = new List<TransferModel>();
                    if (dvTransfer != null)  //查询失败
                    {
                        transferList = allTransferList;
                        transferList = (from x in transferList
                                        where x.EmployeeID == employeeId
                                        select x).ToList();
                    }
                    var transfer = (from x in transferList
                                    where x.TransferDate >= DateTime.Parse(strStartdate)
                                    orderby x.TransferDate ascending
                                    select x).FirstOrDefault();
                    if (transfer != null)
                    {
                        deptid = transfer.OldDeptID;
                        if (transfer.TransferType == "入职")
                            indate = transfer.TransferDate.ToString("yyyy-MM-dd");
                    }
                    for (int i = 0; i <= days; i++)
                    {
                        bool btemp = false;
                        bool bholiday = false;
                        string curdate = startdate.AddDays(i).ToString("yyyy-MM-dd");
                        var _transferList =
                            transferList.FindAll(
                                p => p.TransferDate.Date == DateTime.Parse(curdate).Date);

                        foreach (var _transfer in _transferList)
                        {
                            if (string.IsNullOrEmpty(_transfer.NewDeptID) == false)
                                deptid = _transfer.NewDeptID;
                            string transferDate = _transfer.TransferDate.ToString("yyyy-MM-dd");
                            if (_transfer.TransferType == HumanResource.Util.TransferType.DISMISS)
                            {
                                if (string.IsNullOrEmpty(attEndCheckDate))
                                    outdate = transferDate;
                                else
                                    outdate = attEndCheckDate;
                            }
                            if (_transfer.TransferType == HumanResource.Util.TransferType.RECOVER)
                            {
                                indate = transferDate;
                                outdate = attEndCheckDate = string.Empty;
                            }
                        }
                        if (outdate != null && outdate.Length > 0 && curdate.CompareTo(outdate) >= 0)
                        // && outdate.CompareTo(indate)>=0) //当前日期大于离职日期
                        {
                            //删除离职日期之后的无效日报表记录
                            var whereString =
                                string.Format(
                                    "employeeid='{0}' and reportdate >= '{1}' and (status<> '锁定' or status is null)",
                                    employeeId, outdate);

                            DeleteByCondition(whereString);
                            continue;
                        }
                        if (curdate.CompareTo(indate) < 0)
                        {
                            continue;
                        }
                        //string strSql =
                        //    string.Format(
                        //        "SELECT attdailyreportid,status from t_attdailyreport WHERE employeeid='{0}' and reportdate='{1}'",
                        //        employeeId, curdate);
                        //object[] values = DBAccessFactory.GetDBAccess(null).QueryOneRecord(strSql);
                        dvEmployeeDailyReport.RowFilter = string.Format("reportdate='{0}' and employeeid='{1}'", curdate,employeeId);
                        string reportid = string.Empty;
                        if (dvEmployeeDailyReport.Count > 0)
                        {
                            reportid = dvEmployeeDailyReport[0]["attdailyreportid"].ToString();
                            var status = dvEmployeeDailyReport[0]["status"].ToString();
                            if (status == "锁定")
                                continue;
                        }
                        //if (values != null && values.Length == 2)
                        //{
                        //    reportid = values[0].ToString();
                        //    var status = values[1].ToString();
                        //    if (status == "锁定")
                        //        continue;
                        //}
                        System.Data.DataRowView row = dvResult.AddNew();
                        if (string.IsNullOrEmpty(reportid) == false)
                        {
                            row[att.business.bi.AttDailyReportEnColumn.ATTDAILYREPORTID] = reportid;
                        }
                        row[att.business.bi.AttDailyReportEnColumn.REPORTDATE] = curdate;
                        row[att.business.bi.AttDailyReportEnColumn.EMPLOYEEID] = employeeId;
                        row[att.business.bi.AttDailyReportEnColumn.DAYOFWEEK] = startdate.AddDays(i).ToString("ddd");

                        #region 设置员工当前所属部门

                        if (string.IsNullOrEmpty(deptid) == false)
                        {
                            var _deptname = string.Empty;
                            allDeptList.RowFilter = string.Format("deptid='{0}'", deptid);
                            if (allDeptList.Count > 0)
                            {
                                for (int y = 6, x = 1; y >= x; y--)
                                {
                                    var _parentName = allDeptList[0]["parent" + y + "name"].ToString();
                                    if (string.IsNullOrEmpty(_parentName) == false)
                                    {
                                        _deptname = _parentName;
                                        break;
                                    }
                                }
                            }
                            row["deptid"] = deptid;
                            row["deptname"] = _deptname;
                        }

                        #endregion

                        #region 请假处理

                        //先处理请假的打卡
                        //while (alLeaveCheckOut.Count > 0 &&
                        //       alLeaveCheckOut[0].ToString().Substring(0, 10).CompareTo(curdate) <= 0)
                        //{
                        //    if (alLeaveCheckOut[0].ToString().Substring(0, 10) == curdate)
                        //    {
                        //        row[att.business.bi.AttDailyReportEnColumn.LEAVEOUT1] = alLeaveCheckOut[0];
                        //        alLeaveCheckOut.RemoveAt(0);
                        //        if (alLeaveCheckOut.Count > 0 &&
                        //            alLeaveCheckOut[0].ToString().Substring(0, 10) == curdate)
                        //        {
                        //            row[att.business.bi.AttDailyReportEnColumn.LEAVEOUT2] = alLeaveCheckOut[0];
                        //            alLeaveCheckOut.RemoveAt(0);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        alLeaveCheckOut.RemoveAt(0);
                        //    }
                        //}
                        //while (alLeaveCheckIn.Count > 0 &&
                        //       alLeaveCheckIn[0].ToString().Substring(0, 10).CompareTo(curdate) <= 0)
                        //{
                        //    if (alLeaveCheckIn[0].ToString().Substring(0, 10) == curdate)
                        //    {
                        //        row[att.business.bi.AttDailyReportEnColumn.LEAVEIN1] = alLeaveCheckIn[0];
                        //        alLeaveCheckIn.RemoveAt(0);
                        //        if (alLeaveCheckIn.Count > 0 && alLeaveCheckIn[0].ToString().Substring(0, 10) == curdate)
                        //        {
                        //            row[att.business.bi.AttDailyReportEnColumn.LEAVEIN2] = alLeaveCheckIn[0];
                        //            alLeaveCheckIn.RemoveAt(0);
                        //        }
                        //    }
                        //    else
                        //    {
                        //        alLeaveCheckIn.RemoveAt(0);
                        //    }
                        //}

                        #endregion

                        #region 临时排班

                        if (dvTempshift != null) //存在临时排班
                        {
                            dvTempshift.RowFilter = "employeeid='" + employeeId + "' and startdate='" + curdate + "'";
                            if (dvTempshift.Count > 0)
                            {
                                btemp = true;

                                #region 临时排班

                                //第一个临时排班为休息，则整天都调休了
                                if (!dvTempshift[0][att.business.bi.TempShiftEnColumn.ATTCLASSID].Equals(-1))
                                {
                                    //根据每天有多少次签入签出循环，寻找每次签入或者签出的时间范围对应的第一条有效记录
                                    for (int n = 1; n <= dvTempshift.Count; n++)
                                    {
                                        if (curindex < dvCheckRecord.Count)
                                        {

                                            string classintime =
                                                dvTempshift[n - 1][att.business.bi.AttClassEnColumn.CHECKINTIME]
                                                    .ToString();
                                            //判断时段是否是有效地
                                            if (classintime.Length == 0)
                                            {
                                                row.EndEdit();
                                                continue;
                                            }

                                            string checktime =
                                                dvCheckRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE]
                                                    .ToString() + " " +
                                                dvCheckRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME]
                                                    .ToString();

                                            string beginchecktime =
                                                dvTempshift[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKIN]
                                                    .ToString();
                                            string endchecktime =
                                                dvTempshift[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKIN].ToString
                                                    ();
                                            string endate =
                                                dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.ENDDATE].ToString();
                                            int lateignore =
                                                (int)dvTempshift[n - 1][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                            int leaveignore =
                                                (int)dvTempshift[n - 1][att.business.bi.AttClassEnColumn.LEAVEIGNORE];

                                            if (classintime.CompareTo(beginchecktime) < 0)
                                            {
                                                beginchecktime = startdate.AddDays(i - 1).ToString("yyyy-MM-dd") + " " +
                                                                 beginchecktime;
                                            }
                                            else
                                            {
                                                beginchecktime = curdate + " " + beginchecktime;
                                            }

                                            while (beginchecktime.CompareTo(checktime) > 0)
                                            {
                                                //找到第一条打卡记录
                                                curindex++;
                                                if (curindex == dvCheckRecord.Count)
                                                {
                                                    break;
                                                }
                                                checktime =
                                                    dvCheckRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE]
                                                        .ToString() + " " +
                                                    dvCheckRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME]
                                                        .ToString();
                                            }
                                            if (curindex < dvCheckRecord.Count)
                                            {

                                                #region 临时跨天

                                                if (endchecktime.CompareTo(classintime) < 0)
                                                {
                                                    endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " +
                                                                   endchecktime;
                                                }
                                                else
                                                {
                                                    endchecktime = curdate + " " + endchecktime;
                                                }
                                                if (endchecktime.CompareTo(beginchecktime) < 0)
                                                {
                                                    //跨天
                                                    endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " +
                                                                   endchecktime;
                                                }

                                                #endregion

                                                //if ((curdate + " " + endchecktime).CompareTo(beginchecktime) < 0)
                                                //{
                                                //    endchecktime = endate + " " + endchecktime;
                                                //}
                                                //else
                                                //{
                                                //    endchecktime = curdate + " " + endchecktime;
                                                //}
                                                if (checktime.CompareTo(endchecktime) <= 0)
                                                {
                                                    //找到了第一个签入记录
                                                    if (
                                                        (System.DateTime.Parse(checktime) -
                                                         System.DateTime.Parse(curdate + " " + classintime))
                                                            .TotalMinutes > lateignore)
                                                    {
                                                        row["in" + n.ToString()] = checktime + " ";
                                                    }
                                                    else
                                                    {
                                                        row["in" + n.ToString()] = checktime;
                                                    }
                                                    curindex++;
                                                }
                                                if (curindex < dvCheckRecord.Count)
                                                {


                                                    beginchecktime =
                                                        dvTempshift[n - 1][
                                                            att.business.bi.AttClassEnColumn.STARTCHECKOUT].ToString();
                                                    endchecktime =
                                                        dvTempshift[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKOUT]
                                                            .ToString();
                                                    string classouttime =
                                                        dvTempshift[n - 1][att.business.bi.AttClassEnColumn.CHECKOUTTIME
                                                            ].ToString();

                                                    bool btwoday = (classintime.CompareTo(classouttime) >= 0);

                                                    if (btwoday)
                                                    {
                                                        if (beginchecktime.CompareTo(classouttime) <= 0)
                                                        {
                                                            beginchecktime = endate + " " + beginchecktime;
                                                        }
                                                        else
                                                        {
                                                            beginchecktime = curdate + " " + beginchecktime;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        beginchecktime = curdate + " " + beginchecktime;
                                                    }
                                                    checktime =
                                                        dvCheckRecord[curindex][
                                                            att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() +
                                                        " " +
                                                        dvCheckRecord[curindex][
                                                            att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();

                                                    while (beginchecktime.CompareTo(checktime) > 0)
                                                    {
                                                        curindex++;
                                                        if (curindex == dvCheckRecord.Count)
                                                        {
                                                            break;
                                                        }
                                                        checktime =
                                                            dvCheckRecord[curindex][
                                                                att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() +
                                                            " " +
                                                            dvCheckRecord[curindex][
                                                                att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                                    }
                                                    if (btwoday)
                                                    {
                                                        endchecktime = endate + " " + endchecktime;
                                                    }
                                                    else
                                                    {
                                                        if (classouttime.CompareTo(endchecktime) > 0)
                                                        {
                                                            endchecktime =
                                                                startdate.AddDays(i + 1).ToString("yyyy-MM-dd") +
                                                                " " + endchecktime;
                                                        }
                                                        else
                                                        {
                                                            endchecktime = curdate + " " + endchecktime;
                                                        }
                                                    }

                                                    //有效的开始签退时间 2016-1-11 yehuabin
                                                    var startOutTime = DateTime.Parse(endate + " " + classouttime).AddMinutes(-leaveignore);
                                                    var isEnd = false;//是否遍历超出表
                                                    //避免早退
                                                    var _checktime = DateTime.Parse(checktime);
                                                    if (_checktime < startOutTime)
                                                    {
                                                        var tempIndex = curindex;
                                                        while (DateTime.Parse(beginchecktime) <= _checktime && _checktime < startOutTime && tempIndex < dvCheckRecord.Count)
                                                        {
                                                            tempIndex++;
                                                            if (tempIndex >= dvCheckRecord.Count)
                                                            {
                                                                isEnd = true;
                                                                break;
                                                            }
                                                            _checktime = DateTime.Parse(string.Format("{0} {1}",
                                                           dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKDATE],
                                                           dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKTIME]));
                                                        }
                                                        if (_checktime >= startOutTime && DateTime.Parse(endchecktime) >= _checktime && !isEnd)
                                                        {
                                                            curindex = tempIndex;
                                                            checktime = _checktime.ToString("yyyy-MM-dd HH:mm");
                                                        }
                                                    }



                                                    if (curindex < dvCheckRecord.Count)
                                                    {

                                                        if (checktime.CompareTo(endchecktime) <= 0)
                                                        {
                                                            //找到签出记录
                                                            if (
                                                                (System.DateTime.Parse(endate + " " + classouttime) -
                                                                 System.DateTime.Parse(checktime)).TotalMinutes >
                                                                leaveignore)
                                                            {
                                                                row["out" + n.ToString()] = checktime + " ";
                                                            }
                                                            else
                                                            {
                                                                row["out" + n.ToString()] = checktime;
                                                            }
                                                            curindex++;
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                        row["attclass" + n.ToString()] =
                                            dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.ATTCLASSID];
                                        row["countovertime" + n.ToString()] =
                                            dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.COUNTOVERTIME];
                                    }

                                    string overtime = dvTempshift[0]["countovertime"].ToString();
                                    if (overtime.Length > 0 && int.Parse(overtime) > 0)
                                    {
                                        row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] =
                                            dvTempshift[0]["attclassname"] + "(加)";
                                    }
                                    else
                                    {
                                        row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] =
                                            dvTempshift[0]["attclassname"];
                                    }

                                    for (int n = 0; n < dvTempshift.Count - 1; n++)
                                    {
                                        int j = n + 1;

                                        overtime = dvTempshift[j]["countovertime"].ToString();
                                        if (overtime.Length > 0 && int.Parse(overtime) > 0)
                                        {
                                            row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] += "/" +
                                                                                                     dvTempshift[j][
                                                                                                         "attclassname"] +
                                                                                                     "(加)";
                                        }
                                        else
                                        {
                                            row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] += "/" +
                                                                                                     dvTempshift[j][
                                                                                                         "attclassname"];
                                        }

                                        if (row["in" + j.ToString()].ToString().Length == 0 &&
                                            row["out" + j.ToString()].ToString().Length > 0 &&
                                            row["in" + (j + 1).ToString()].ToString().Length == 0 &&
                                            row["out" + (j + 1).ToString()].ToString().Length > 0)
                                        {
                                            string checktime = row["out" + j.ToString()].ToString();
                                            string startchecktime = curdate + " " +
                                                                    dvTempshift[n + 1][
                                                                        att.business.bi.AttClassEnColumn.STARTCHECKIN];
                                            if (checktime.CompareTo(startchecktime) >= 0)
                                            {
                                                row["in" + (j + 1).ToString()] = row["out" + j.ToString()];
                                                row["out" + j.ToString()] = System.DBNull.Value;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] =
                                        att.util.ShiftType.TEMPSHIFT + "(" + att.util.ShiftType.HOLIDAY + ")";
                                }

                                #endregion
                            }
                        }

                        #endregion

                        #region 节假日

                        //if (!btemp && bHasHoliday) //节假日并且不存在临时排班
                        //{
                        //    //假期处理
                        //    System.Data.DataRow[] drs =
                        //        dvHoliday.Table.Select("starttime <='" + curdate + "' and endtime>='" + curdate + "'");
                        //    if (drs.Length > 0)
                        //    {
                        //        foreach (var drv in drs)
                        //        {
                        //            var holidayType = drv[att.business.bi.HolidayEnColumn.HOLIDAYTYPE].ToString();
                        //            var holidayName = drv[att.business.bi.HolidayEnColumn.HOLIDAYNAME].ToString();
                        //            var deptids = drv[att.business.bi.HolidayEnColumn.APPLYDEPTIDS].ToString();

                        //            if (string.IsNullOrEmpty(deptids) == false) //假期适用范围
                        //            {
                        //                if (string.IsNullOrEmpty(employeebi.DEPTID) == true)
                        //                    continue;

                        //                var result = deptids.Split(',').Contains(employeebi.DEPTID);
                        //                if (result == false)
                        //                    continue;
                        //            }
                        //            row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = holidayType + "-" +
                        //                                                                    holidayName;
                        //            bholiday = true;
                        //        }
                        //    }
                        //}

                        #endregion

                        #region 员工排班
                        var dvempshift = dvempshiftAll.Table.Copy().DefaultView;
                        dvempshift.RowFilter = ("employeeid='" + employeeId + "' and  startdate<='" + curdate + "' and enddate>='" + curdate + "'");

                        #region 查找部门排次,5ikq定制,东东
                        if (dvempshift.Count == 0)
                        {
                            //"SELECT employeeshiftid,employeeid,shiftid,shifttype,shiftname,startdate,enddate,cycle,cycleunit FROM v_employeeshift";
                            if (string.IsNullOrEmpty(deptid) == false)
                            {
                                var currentDate = DateTime.Parse(curdate);
                                var deptAdvShift = dvdeptshift.FirstOrDefault(p => p.DeptID == deptid && p.StartDate <= currentDate && p.EndDate >= currentDate);
                                if (deptAdvShift != null)   //部门排班列表（高级）
                                {
                                    var shift = att.business.ABOFactory.BOFactory.GetShiftBI();
                                    shift.SHIFTID = deptAdvShift.ShiftID;
                                    if (shift.Retrieve() == true)
                                    {
                                        dvempshift.Table.Clear();
                                        var drv = dvempshift.AddNew();
                                        drv["employeeid"] = employeeId;
                                        drv["shiftid"] = shift.SHIFTID;
                                        drv["shifttype"] = shift.SHIFTTYPE;
                                        drv["shiftname"] = shift.SHIFTNAME;
                                        drv["startdate"] = deptAdvShift.StartDate.ToString("yyyy-MM-dd");
                                        drv["enddate"] = deptAdvShift.EndDate.ToString("yyyy-MM-dd");
                                        drv["cycle"] = shift.CYCLE;
                                        drv["cycleunit"] = shift.CYCLEUNIT;
                                        drv.EndEdit();
                                    }
                                }
                                else    //默认部门排班
                                {
                                    var deptShift = deptShifts.FindDepartmentShiftByDeptID(deptid);
                                    if (deptShift != null && deptShift.ShiftInfo != null)
                                    {
                                        dvempshift.Table.Clear();
                                        var drv = dvempshift.AddNew();
                                        drv["employeeid"] = employeeId;
                                        drv["shiftid"] = deptShift.ShiftInfo.ShiftID;
                                        drv["shifttype"] = att.util.ShiftType.SHIFT;
                                        drv["shiftname"] = deptShift.ShiftInfo.ShiftName;
                                        drv["startdate"] = DateTime.MinValue;
                                        drv["enddate"] = DateTime.MaxValue;
                                        drv["cycle"] = 1;
                                        drv["cycleunit"] = att.util.ShiftCycleUnit.WEEK;
                                        drv.EndEdit();
                                    }
                                }
                            }
                        }
                        #endregion
                        bool hasshift = (dvempshift != null && dvempshift.Count > 0);

                        if (hasshift && !btemp && !bholiday) //存在班次，且无临时排班和节假日
                        {
                            System.Data.DataRow[] drs =
                                dvempshift.Table.Select("employeeid='" + employeeId + "' and startdate<='" + curdate + "' and enddate>='" + curdate + "'");
                            if (drs.Length > 0)
                            {
                                string _shifttype = drs[0][att.business.bi.EmployeeShiftEnColumn.SHIFTTYPE].ToString();
                                switch (_shifttype)
                                {
                                    case att.util.ShiftType.SHIFT:
                                        {
                                            #region 规律排班

                                            int dayspan = 0;
                                            switch (drs[0]["cycleunit"].ToString())
                                            {
                                                case att.util.ShiftCycleUnit.DAY:
                                                case "日":
                                                    {
                                                        dayspan =
                                                            (startdate -
                                                             System.DateTime.Parse(
                                                                 drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE]
                                                                     .ToString())).Days + i;
                                                        dayspan = dayspan % int.Parse(drs[0]["cycle"].ToString());
                                                    }
                                                    break;
                                                case att.util.ShiftCycleUnit.WEEK:
                                                    {
                                                        System.DateTime dt =
                                                            System.DateTime.Parse(
                                                                drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE].ToString
                                                                    ());

                                                        dayspan = (startdate - dt).Days + i + dt.DayOfWeek -
                                                                  System.DayOfWeek.Monday;
                                                        int icycle = ((int)drs[0]["cycle"] * 7);
                                                        dayspan = dayspan % icycle;
                                                        if (dayspan == -1)
                                                        {
                                                            dayspan = icycle - 1;
                                                        }
                                                    }
                                                    break;
                                                case att.util.ShiftCycleUnit.MONTH:
                                                    {
                                                        System.DateTime dt =
                                                            System.DateTime.Parse(
                                                                drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE].ToString
                                                                    ());
                                                        int monthspan = (startdate.Month - dt.Month) %
                                                                        int.Parse(drs[0]["cycle"].ToString());
                                                        dayspan = startdate.Day + i + monthspan * 31 - 1;
                                                    }
                                                    break;
                                            }
                                            if (shiftid != drs[0]["shiftid"].ToString())
                                            {
                                                shiftid = drs[0]["shiftid"].ToString();
                                                dvShiftDetail.RowFilter = "shiftid='" + drs[0]["shiftid"] + "'";
                                                dvDetail = dvShiftDetail.ToTable().DefaultView;
                                            }
                                            dvDetail.RowFilter = "startday=" + dayspan;
                                            //dvDetail.Sort = att.business.bi.AttClassEnColumn.CHECKINTIME;
                                            #region 找到部门排班 找签卡记录
                                            if (dvDetail.Count > 0)
                                            {
                                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = drs[0]["shiftname"];
                                                //根据每天有多少次签入签出循环

                                                for (int n = 1; n <= dvDetail.Count; n++)
                                                {
                                                    if (curindex < dvCheckRecord.Count)
                                                    {
                                                        string beginchecktime =
                                                            dvDetail[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKIN]
                                                                .ToString();
                                                        DateTime _checktime = DateTime.Parse(
                                                            string.Format("{0} {1}",
                                                                dvCheckRecord[curindex][
                                                                    att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(),
                                                                dvCheckRecord[curindex][
                                                                    att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));

                                                        string endchecktime =
                                                            dvDetail[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKIN]
                                                                .ToString();
                                                        string classintime =
                                                            dvDetail[n - 1][att.business.bi.AttClassEnColumn.CHECKINTIME]
                                                                .ToString();
                                                        int lateignore =
                                                            (int)
                                                                dvDetail[n - 1][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                                        int leaveignore =
                                                            (int)
                                                                dvDetail[n - 1][att.business.bi.AttClassEnColumn.LEAVEIGNORE
                                                                    ];

                                                        if (classintime.CompareTo(beginchecktime) < 0)
                                                        {
                                                            beginchecktime =
                                                                startdate.AddDays(i - 1).ToString("yyyy-MM-dd") + " " +
                                                                beginchecktime;
                                                        }
                                                        else
                                                        {
                                                            beginchecktime = curdate + " " + beginchecktime;
                                                        }
                                                        while (DateTime.Parse(beginchecktime) > _checktime)
                                                        {
                                                            //找签入记录
                                                            curindex++;
                                                            if (curindex == dvCheckRecord.Count)
                                                            {
                                                                break;
                                                            }
                                                            _checktime = DateTime.Parse(
                                                                string.Format("{0} {1}",
                                                                    dvCheckRecord[curindex][
                                                                        att.business.bi.AttRecordEnColumn.CHECKDATE]
                                                                        .ToString(),
                                                                    dvCheckRecord[curindex][
                                                                        att.business.bi.AttRecordEnColumn.CHECKTIME]
                                                                        .ToString()));
                                                        }
                                                        if (curindex < dvCheckRecord.Count)
                                                        {
                                                            if (endchecktime.CompareTo(classintime) < 0)
                                                            {
                                                                endchecktime =
                                                                    startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " +
                                                                    endchecktime;
                                                            }
                                                            else
                                                            {
                                                                endchecktime = curdate + " " + endchecktime;
                                                            }
                                                            if (endchecktime.CompareTo(beginchecktime) < 0)
                                                            {
                                                                //跨天
                                                                endchecktime =
                                                                    startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " +
                                                                    endchecktime;
                                                            }
                                                            if (_checktime <= DateTime.Parse(endchecktime))
                                                            {
                                                                //找到签入记录
                                                                {
                                                                    row["in" + n.ToString()] =
                                                                        _checktime.ToString((_checktime.Second != 0)
                                                                            ? "yyyy-MM-dd HH:mm:ss"
                                                                            : "yyyy-MM-dd HH:mm");
                                                                    if (
                                                                        (_checktime -
                                                                         System.DateTime.Parse(curdate + " " + classintime))
                                                                            .TotalMinutes > lateignore)
                                                                    {
                                                                        row["in" + n.ToString()] += " ";
                                                                    }
                                                                }
                                                                curindex++;
                                                            }
                                                            if (curindex < dvCheckRecord.Count)
                                                            {
                                                                beginchecktime =
                                                                    dvDetail[n - 1][
                                                                        att.business.bi.AttClassEnColumn.STARTCHECKOUT]
                                                                        .ToString();
                                                                endchecktime =
                                                                    dvDetail[n - 1][
                                                                        att.business.bi.AttClassEnColumn.ENDCHECKOUT]
                                                                        .ToString();
                                                                string classouttime =
                                                                    dvDetail[n - 1][
                                                                        att.business.bi.AttClassEnColumn.CHECKOUTTIME]
                                                                        .ToString();

                                                                bool btwoday =
                                                                    (classouttime.CompareTo(
                                                                        dvDetail[n - 1][
                                                                            att.business.bi.AttClassEnColumn.CHECKINTIME]) <=
                                                                     0);
                                                                if (btwoday)
                                                                {
                                                                    if (beginchecktime.CompareTo(classouttime) <= 0)
                                                                    {
                                                                        beginchecktime =
                                                                            startdate.AddDays(i + 1).ToString("yyyy-MM-dd") +
                                                                            " " + beginchecktime;
                                                                    }
                                                                    else
                                                                    {
                                                                        beginchecktime = curdate + " " + beginchecktime;
                                                                    }
                                                                    classouttime =
                                                                        startdate.AddDays(i + 1).ToString("yyyy-MM-dd") +
                                                                        " " + classouttime;
                                                                }
                                                                else
                                                                {
                                                                    beginchecktime = curdate + " " + beginchecktime;
                                                                    classouttime = curdate + " " + classouttime;
                                                                }
                                                                _checktime = DateTime.Parse(
                                                                    string.Format("{0} {1}",
                                                                        dvCheckRecord[curindex][
                                                                            att.business.bi.AttRecordEnColumn.CHECKDATE]
                                                                            .ToString(),
                                                                        dvCheckRecord[curindex][
                                                                            att.business.bi.AttRecordEnColumn.CHECKTIME]
                                                                            .ToString()));
                                                                while (DateTime.Parse(beginchecktime) > _checktime)
                                                                {
                                                                    //找签出记录
                                                                    curindex++;
                                                                    if (curindex == dvCheckRecord.Count)
                                                                    {
                                                                        break;
                                                                    }
                                                                    _checktime = DateTime.Parse(string.Format("{0} {1}",
                                                                        dvCheckRecord[curindex][
                                                                            att.business.bi.AttRecordEnColumn.CHECKDATE]
                                                                            .ToString(),
                                                                        dvCheckRecord[curindex][
                                                                            att.business.bi.AttRecordEnColumn.CHECKTIME]
                                                                            .ToString()));
                                                                }
                                                                if (btwoday)
                                                                {
                                                                    endchecktime =
                                                                        startdate.AddDays(i + 1).ToString("yyyy-MM-dd") +
                                                                        " " + endchecktime;
                                                                }
                                                                else
                                                                {
                                                                    if (
                                                                        classouttime.CompareTo(curdate + " " +
                                                                                               endchecktime) > 0)
                                                                    {
                                                                        btwoday = true;
                                                                        endchecktime =
                                                                            startdate.AddDays(i + 1)
                                                                                .ToString("yyyy-MM-dd") + " " +
                                                                            endchecktime;
                                                                    }
                                                                    else
                                                                    {
                                                                        endchecktime = curdate + " " + endchecktime;
                                                                    }
                                                                }

                                                                //有效的开始签退时间 2016-1-11 yehuabin
                                                                var startOutTime = DateTime.Parse(classouttime).AddMinutes(-leaveignore);
                                                                var isEnd = false;//是否遍历超出表
                                                                //避免早退
                                                                if (_checktime < startOutTime)
                                                                {
                                                                    var tempIndex = curindex;
                                                                    var tempChecktime = _checktime;
                                                                    while (DateTime.Parse(beginchecktime) <= tempChecktime && tempChecktime < startOutTime)
                                                                    {
                                                                        tempIndex++;
                                                                        if (tempIndex >= dvCheckRecord.Count)
                                                                        {
                                                                            isEnd = true;
                                                                            break;
                                                                        }
                                                                        tempChecktime = DateTime.Parse(string.Format("{0} {1}",
                                                                       dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKDATE],
                                                                       dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKTIME]));
                                                                    }
                                                                    if (tempChecktime >= startOutTime &&
                                                                        tempChecktime <= DateTime.Parse(endchecktime) && !isEnd)
                                                                    {
                                                                        //找到正常签退记录
                                                                        curindex = tempIndex;
                                                                        _checktime = tempChecktime;
                                                                    }
                                                                    //else
                                                                    //{
                                                                    //    //早退取最后一条早退记录
                                                                    //    tempIndex = curindex;
                                                                    //    tempChecktime = _checktime;
                                                                    //    while (DateTime.Parse(beginchecktime) <= tempChecktime && tempChecktime <= DateTime.Parse(endchecktime) && tempIndex < dvCheckRecord.Count)
                                                                    //    {
                                                                    //        tempIndex++;
                                                                    //        if (tempIndex >= dvCheckRecord.Count)
                                                                    //        {
                                                                    //            break;
                                                                    //        }
                                                                    //        tempChecktime = DateTime.Parse(string.Format("{0} {1}",
                                                                    //        dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKDATE],
                                                                    //        dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKTIME]));
                                                                    //    }
                                                                    //    if (tempIndex != curindex)
                                                                    //    {
                                                                    //        //取最后一条早退
                                                                    //        tempIndex--;
                                                                    //        curindex = tempIndex;
                                                                    //        _checktime = DateTime.Parse(string.Format("{0} {1}",
                                                                    //        dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKDATE],
                                                                    //        dvCheckRecord[tempIndex][AttRecordEnColumn.CHECKTIME]));
                                                                    //    }
                                                                    //}
                                                                }

                                                                if (curindex < dvCheckRecord.Count)
                                                                {

                                                                    if (_checktime <= DateTime.Parse(endchecktime))
                                                                    {
                                                                        //找到签出记录
                                                                        {
                                                                            row["out" + n.ToString()] =
                                                                                _checktime.ToString((_checktime.Second != 0)
                                                                                    ? "yyyy-MM-dd HH:mm:ss"
                                                                                    : "yyyy-MM-dd HH:mm");
                                                                            if (
                                                                                (System.DateTime.Parse(classouttime) -
                                                                                 _checktime).TotalMinutes > leaveignore)
                                                                            {
                                                                                row["out" + n.ToString()] += " ";
                                                                            }
                                                                        }
                                                                        curindex++;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }//end if (curindex < dvCheckRecord.Count)
                                                    row["attclass" + n.ToString()] =
                                                        dvDetail[n - 1][att.business.bi.ShiftDetailEnColumn.ATTCLASSID];
                                                    row["countovertime" + n.ToString()] =
                                                        dvDetail[n - 1][
                                                            att.business.bi.ShiftDetailEnColumn.COUNTOVERTIME];
                                                }//end for n<=dvDetail.Count
                                                //把缺少签入的签出记录移到下个时段的签入中使用
                                                for (int n = 0; n < dvDetail.Count - 1; n++)
                                                {
                                                    int j = n + 1;
                                                    if (row["in" + j.ToString()].ToString().Length == 0 &&
                                                        row["out" + j.ToString()].ToString().Length > 0 &&
                                                        row["in" + (j + 1).ToString()].ToString().Length == 0 &&
                                                        row["out" + (j + 1).ToString()].ToString().Length > 0)
                                                    {
                                                        string checktime = row["out" + j.ToString()].ToString();
                                                        string startchecktime = curdate + " " +
                                                                                dvDetail[n + 1][
                                                                                    att.business.bi.AttClassEnColumn
                                                                                        .STARTCHECKIN];
                                                        if (checktime.CompareTo(startchecktime) >= 0)
                                                        {
                                                            row["in" + (j + 1).ToString()] = row["out" + j.ToString()];
                                                            row["out" + j.ToString()] = System.DBNull.Value;
                                                        }
                                                    }
                                                }
                                            }//end dvDetail.Count>0
                                            #endregion
                                            else
                                            {
                                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] =
                                                    att.util.ShiftType.HOLIDAY;
                                            }

                                            #endregion
                                        }
                                        break;
                                    case att.util.ShiftType.AUTOSHIFT:
                                        {
                                            throw new MessageException("暂不支持类型:智能班组");
                                            break;
                                        }
                                    case att.util.ShiftType.FREESHIFT:
                                        throw new MessageException("暂不支持类型:自由排班");
                                        break;
                                    case att.util.ShiftType.AUTOSHIFTLIST:
                                        throw new MessageException("暂不支持类型:智能班次组合");
                                        break;
                                }
                            }
                        }

                        #endregion

                        row.EndEdit();
                    }
                    foreach (System.Data.DataRowView drv in dvResult)
                    {
                        var reportid = drv[att.business.bi.AttDailyReportEnColumn.ATTDAILYREPORTID].ToString();
                        if (String.IsNullOrEmpty(reportid) == false)
                        {
                            drv.Row.AcceptChanges();
                            drv.Row.SetModified(); //修改为编辑状态
                        }
                    }
                    dvEmployeeDailyReport.RowFilter = string.Format("employeeid='{0}'", employeeId);
                    var reslt = this.SaveRows(dvResult, dvEmployeeDailyReport);

                    humanresource.BusinessLogger.EndAction(employeeId + "日报表生成完毕，用时：" +
                                                           (DateTime.Now - _log_startDate).ToString()); //记录日志

                }
                catch (Exception ex)
                {
                    this.ProcessException("GenerateDailyReportRecord生成日报表", ex.ToString());
                    humanresource.BusinessLogger.Error("生成日报表出错", ex);
                }
            }
            return true;
        }
