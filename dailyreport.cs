   public virtual bool GenerateDailyReportRecord(System.DateTime startdate, System.DateTime enddate, string employeeid)
        {
            var _log_startDate = DateTime.Now; //仅供日志使用
            humanresource.BusinessLogger.BeginAction(startdate, enddate, employeeid); //记录日志

            if (startdate > enddate)
            {
                return false;
            }
            int days = (enddate - startdate).Days;
            string strStartdate, strEnddate;
            strStartdate = startdate.ToString("yyyy-MM-dd");
            strEnddate = enddate.ToString("yyyy-MM-dd");
            employeebi.IDValue = employeeid;

            string indate = null, outdate = string.Empty, attEndCheckDate = string.Empty;
            if (employeebi.Retrieve() == false)
            {
                etier.business.Log.AddLog("生成考勤日报表出错：员工信息不存在或已被删除" + employeeid);
                return false;
            }
            #region 获取员工入职日期和考勤截止日日期
            indate = employeebi.STARTDATE;
            if (string.IsNullOrEmpty(employeebi.OUTDATE) == false)
            {
                outdate = employeebi.OUTDATE;
            }
            //人事异动表
            var transferList = _transferbi.GetTransferList(employeeid);
            var indexTransfer = transferList.FindIndex(p => p.TransferType == HumanResource.Util.TransferType.DISMISS && p.EmployeeID == employeeid && p.TransferDate >= startdate);
            if (indexTransfer != -1)
            {
                indate = strStartdate;
            }

            System.Data.DataView dvcustomercolumn = _customercolumnbi.GetTableValue(employeebi.IDValue);
            if (dvcustomercolumn != null && dvcustomercolumn.Count == 1 && dvcustomercolumn.Table.Columns.IndexOf(HumanResource.Util.CustomerColumnNames.ATTENDANCEENDDATE) >= 0)
            {
                string tempdate = dvcustomercolumn[0][HumanResource.Util.CustomerColumnNames.ATTENDANCEENDDATE].ToString();
                if (tempdate.Length > 0)
                {
                    outdate = attEndCheckDate = tempdate;
                }
            }
            #endregion


            try
            {
                #region 获取签卡记录列表、临时排班列表、员工班排列表、节假日列表、请假列表、员工异动表
                System.Data.DataView dvResult = base.QueryByCondition("1=2");
                var dvAttClass = att.business.ABOFactory.BOFactory.GetAttClassBI().GetAttClassAllList();
                string lastchecktime = this.GetLastCheckRecord(employeeid, startdate);
                System.Data.DataView dvTempshift = tempbo.QueryByCondition("employeeid='" + employeeid + "' and startdate>='" + strStartdate + "' and startdate<='" + strEnddate + "'");
                System.Data.DataView dvAttRecord;
                int curindex = 0;
                if (lastchecktime == null)
                {
                    dvAttRecord = recordbo.QueryByCondition("employeeid='" + employeeid + "' and checkdate>='" + startdate.AddDays(-1).ToString("yyyy-MM-dd") + "' and checkdate<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") + "' order by checkdate,checktime");
                }
                else
                {
                    dvAttRecord = recordbo.QueryByCondition("employeeid='" + employeeid + "' and checkdate+' '+checktime>'" + lastchecktime + "' and checkdate<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") + "' order by checkdate,checktime");
                    //curindex = 1;
                }



                System.Data.DataView dvempshift = employeeshiftbo.QueryByCondition("employeeid='" + employeeid + "' and startdate<='" + strEnddate + "' and enddate>='" + strStartdate + "'");
                bool hasshift = (dvempshift != null && dvempshift.Count > 0);
                bool hastemp = (dvTempshift != null & dvTempshift.Count > 0);

                System.Data.DataView dvHoliday = this._holidaybi.QueryByCondition("endtime>='" + startdate.ToString("yyyy-MM-dd") + "' and starttime<='" + enddate.AddDays(1).ToString("yyyy-MM-dd") + "'");
                System.Data.DataView dvLeavebill = this.attLeaveBillBI.QueryByCondition("employeeid='" + employeeid + "' and endtime>='" + startdate.ToString("yyyy-MM-dd 00:00:00") + "' and starttime<='" + enddate.AddDays(1).ToString("yyyy-MM-dd 23:59:59") + "' and (needcheckout='是' or needcheckin='是')");
                System.Collections.ArrayList alLeaveCheckOut = new System.Collections.ArrayList();
                System.Collections.ArrayList alLeaveCheckIn = new System.Collections.ArrayList();


                //清除小于打开间隔的打卡记录
                if (dvAttRecord.Count > 0)
                {
                    if (this.AttParams.IntervalMinute > 0)
                    {
                        int i = 1;
                        DateTime time = DateTime.Parse(dvAttRecord[0][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[0][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString());
                        while (i < dvAttRecord.Count)
                        {
                            DateTime ctime = DateTime.Parse(dvAttRecord[i][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[i][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString());
                            System.TimeSpan ts = new TimeSpan(0);
                            if ((ctime - time).TotalMinutes < this.AttParams.IntervalMinute)
                            {
                                dvAttRecord[i].Delete();
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
                        string time = dvAttRecord[0][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[0][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                        while (i < dvAttRecord.Count)
                        {
                            string ctime = dvAttRecord[i][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[i][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                            if (time == ctime)
                            {
                                dvAttRecord[i].Delete();
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
                if (dvLeavebill != null && dvLeavebill.Count > 0)
                {
                    dvLeavebill.Sort = "starttime";
                    for (int i = 0; i < dvLeavebill.Count; i++)
                    {
                        if (dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.NEEDCHECKOUT].Equals("是"))
                        {
                            //请假开始时间
                            DateTime starttime = DateTime.Parse(dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.STARTTIME].ToString());

                            //请假开始签卡时间
                            DateTime checkstarttime = starttime.AddMinutes(-this.AttParams.GetValueAsInt("请假允许提前多少分钟签出"));
                            //请假结束签卡时间
                            DateTime checkendtime = starttime.AddMinutes(this.AttParams.LeaveCheckoutLater);

                            dvAttRecord.RowFilter = "checkdate+' '+checktime>='" + checkstarttime.ToString("yyyy-MM-dd HH:mm") + "' and checkdate+' '+checktime<='" + checkendtime.ToString("yyyy-MM-dd HH:mm") + "'";
                            if (dvAttRecord.Count > 0)
                            {
                                alLeaveCheckOut.Add(dvAttRecord[0]["checkdate"].ToString() + " " + dvAttRecord[0]["checktime"].ToString());
                                dvAttRecord.Delete(0);
                            }
                        }
                        if (dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.NEEDCHECKIN].Equals("是"))
                        {
                            //请假结束时间
                            DateTime endtime = DateTime.Parse(dvLeavebill[i][att.business.bi.AttLeaveBillEnColumn.ENDTIME].ToString());

                            //请假开始签卡时间
                            DateTime checkstarttime = endtime.AddMinutes(0 - this.AttParams.LeaveCheckinEarlier);
                            //请假结束签卡时间
                            DateTime checkendtime = endtime.AddMinutes(this.AttParams.GetValueAsInt("请假允许延后多少分钟签入"));

                            dvAttRecord.RowFilter = "checkdate+' '+checktime>='" + checkstarttime.ToString("yyyy-MM-dd HH:mm") + "' and checkdate+' '+checktime<='" + checkendtime.ToString("yyyy-MM-dd HH:mm") + "'";
                            if (dvAttRecord.Count > 0)
                            {
                                alLeaveCheckIn.Add(dvAttRecord[dvAttRecord.Count - 1]["checkdate"].ToString() + " " + dvAttRecord[dvAttRecord.Count - 1]["checktime"].ToString());
                                dvAttRecord.Delete(dvAttRecord.Count - 1);
                            }
                        }
                    }
                    dvAttRecord.RowFilter = null;
                }
                #endregion

                bool bHasHoliday = (dvHoliday.Count > 0);
                string shiftid = null;
                DataView dvDetail = null;


                var deptid = employeebi.DEPTID;
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
                    var _transferList = transferList.FindAll(p => p.EmployeeID == employeeid && p.TransferDate.Date == DateTime.Parse(curdate).Date);

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
                    if (outdate != null && outdate.Length > 0 && curdate.CompareTo(outdate) >= 0)// && outdate.CompareTo(indate)>=0) //当前日期大于离职日期
                    {
                        //删除离职日期之后的无效日报表记录
                        var whereString = string.Format("employeeid='{0}' and reportdate >= '{1}' and (status<> '锁定' or status is null)", employeeid, outdate);

                        DeleteByCondition(whereString);
                        continue;
                    }
                    if (curdate.CompareTo(indate) < 0)
                    {
                        continue;
                    }
                    string strSql = string.Format("SELECT attdailyreportid,status from t_attdailyreport WHERE employeeid='{0}' and reportdate='{1}'", employeeid, curdate);
                    object[] values = DBAccessFactory.GetDBAccess(null).QueryOneRecord(strSql);

                    string reportid = string.Empty;
                    if (values != null && values.Length == 2)
                    {
                        reportid = values[0].ToString();
                        var status = values[1].ToString();
                        if (status == "锁定")
                            continue;
                    }
                    System.Data.DataRowView row = dvResult.AddNew();
                    if (string.IsNullOrEmpty(reportid) == false)
                    {
                        row[att.business.bi.AttDailyReportEnColumn.ATTDAILYREPORTID] = reportid;
                    }
                    row[att.business.bi.AttDailyReportEnColumn.REPORTDATE] = curdate;
                    row[att.business.bi.AttDailyReportEnColumn.EMPLOYEEID] = employeeid;
                    row[att.business.bi.AttDailyReportEnColumn.DAYOFWEEK] = startdate.AddDays(i).ToString("ddd");


                    #region 设置员工当前所属部门
                    if (string.IsNullOrEmpty(deptid) == false)
                    {
                        if (allDeptList == null)
                        {
                            var parentDeptBI = etier.business.DBBusinessFactory.BusinessFactory.CreateParentDeptsBI();
                            allDeptList = parentDeptBI.QueryByCondition(null);
                        }
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

                    //先处理请假的打卡
                    while (alLeaveCheckOut.Count > 0 && alLeaveCheckOut[0].ToString().Substring(0, 10).CompareTo(curdate) <= 0)
                    {
                        if (alLeaveCheckOut[0].ToString().Substring(0, 10) == curdate)
                        {
                            row[att.business.bi.AttDailyReportEnColumn.LEAVEOUT1] = alLeaveCheckOut[0];
                            alLeaveCheckOut.RemoveAt(0);
                            if (alLeaveCheckOut.Count > 0 && alLeaveCheckOut[0].ToString().Substring(0, 10) == curdate)
                            {
                                row[att.business.bi.AttDailyReportEnColumn.LEAVEOUT2] = alLeaveCheckOut[0];
                                alLeaveCheckOut.RemoveAt(0);
                            }
                        }
                        else
                        {
                            alLeaveCheckOut.RemoveAt(0);
                        }
                    }
                    while (alLeaveCheckIn.Count > 0 && alLeaveCheckIn[0].ToString().Substring(0, 10).CompareTo(curdate) <= 0)
                    {
                        if (alLeaveCheckIn[0].ToString().Substring(0, 10) == curdate)
                        {
                            row[att.business.bi.AttDailyReportEnColumn.LEAVEIN1] = alLeaveCheckIn[0];
                            alLeaveCheckIn.RemoveAt(0);
                            if (alLeaveCheckIn.Count > 0 && alLeaveCheckIn[0].ToString().Substring(0, 10) == curdate)
                            {
                                row[att.business.bi.AttDailyReportEnColumn.LEAVEIN2] = alLeaveCheckIn[0];
                                alLeaveCheckIn.RemoveAt(0);
                            }
                        }
                        else
                        {
                            alLeaveCheckIn.RemoveAt(0);
                        }
                    }
                    if (hastemp)   //存在临时排班
                    {
                        dvTempshift.RowFilter = "startdate='" + curdate + "'";
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
                                    if (curindex < dvAttRecord.Count)
                                    {

                                        string classintime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.CHECKINTIME].ToString();
                                        //判断时段是否是有效地
                                        if (classintime.Length == 0)
                                        {
                                            row.EndEdit();
                                            continue;
                                        }

                                        string checktime = dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();

                                        string beginchecktime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKIN].ToString();
                                        string endchecktime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKIN].ToString();
                                        string endate = dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.ENDDATE].ToString();
                                        int lateignore = (int)dvTempshift[n - 1][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                        int leaveignore = (int)dvTempshift[n - 1][att.business.bi.AttClassEnColumn.LEAVEIGNORE];

                                        if (classintime.CompareTo(beginchecktime) < 0)
                                        {
                                            beginchecktime = startdate.AddDays(i - 1).ToString("yyyy-MM-dd") + " " + beginchecktime;
                                        }
                                        else
                                        {
                                            beginchecktime = curdate + " " + beginchecktime;
                                        }

                                        while (beginchecktime.CompareTo(checktime) > 0)
                                        {
                                            //找到第一条打卡记录
                                            curindex++;
                                            if (curindex == dvAttRecord.Count)
                                            {
                                                break;
                                            }
                                            checktime = dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                        }
                                        if (curindex < dvAttRecord.Count)
                                        {

                                            #region 临时跨天
                                            if (endchecktime.CompareTo(classintime) < 0)
                                            {
                                                endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                            }
                                            else
                                            {
                                                endchecktime = curdate + " " + endchecktime;
                                            }
                                            if (endchecktime.CompareTo(beginchecktime) < 0)
                                            {
                                                //跨天
                                                endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
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
                                                if ((System.DateTime.Parse(checktime) - System.DateTime.Parse(curdate + " " + classintime)).TotalMinutes > lateignore)
                                                {
                                                    row["in" + n.ToString()] = checktime + " ";
                                                }
                                                else
                                                {
                                                    row["in" + n.ToString()] = checktime;
                                                }
                                                curindex++;
                                            }
                                            if (curindex < dvAttRecord.Count)
                                            {


                                                beginchecktime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKOUT].ToString();
                                                endchecktime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKOUT].ToString();
                                                string classouttime = dvTempshift[n - 1][att.business.bi.AttClassEnColumn.CHECKOUTTIME].ToString();

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
                                                checktime = dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();

                                                while (beginchecktime.CompareTo(checktime) > 0)
                                                {
                                                    curindex++;
                                                    if (curindex == dvAttRecord.Count)
                                                    {
                                                        break;
                                                    }
                                                    checktime = dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                                }

                                                if (curindex < dvAttRecord.Count)
                                                {
                                                    if (btwoday)
                                                    {
                                                        endchecktime = endate + " " + endchecktime;
                                                    }
                                                    else
                                                    {
                                                        if (classouttime.CompareTo(endchecktime) > 0)
                                                        {
                                                            endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                                        }
                                                        else
                                                        {
                                                            endchecktime = curdate + " " + endchecktime;
                                                        }
                                                    }
                                                    if (checktime.CompareTo(endchecktime) <= 0)
                                                    {
                                                        //找到签出记录
                                                        if ((System.DateTime.Parse(endate + " " + classouttime) - System.DateTime.Parse(checktime)).TotalMinutes > leaveignore)
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
                                    row["attclass" + n.ToString()] = dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.ATTCLASSID];
                                    row["countovertime" + n.ToString()] = dvTempshift[n - 1][att.business.bi.TempShiftEnColumn.COUNTOVERTIME];
                                }

                                string overtime = dvTempshift[0]["countovertime"].ToString();
                                if (overtime.Length > 0 && int.Parse(overtime) > 0)
                                {
                                    row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = dvTempshift[0]["attclassname"] + "(加)";
                                }
                                else
                                {
                                    row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = dvTempshift[0]["attclassname"];
                                }
                                //把缺少签入的签出记录移到下个时段的签入中使用
                                for (int n = 0; n < dvTempshift.Count - 1; n++)
                                {
                                    int j = n + 1;

                                    overtime = dvTempshift[j]["countovertime"].ToString();
                                    if (overtime.Length > 0 && int.Parse(overtime) > 0)
                                    {
                                        row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] += "/" + dvTempshift[j]["attclassname"] + "(加)";
                                    }
                                    else
                                    {
                                        row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] += "/" + dvTempshift[j]["attclassname"];
                                    }

                                    if (row["in" + j.ToString()].ToString().Length == 0 && row["out" + j.ToString()].ToString().Length > 0 && row["in" + (j + 1).ToString()].ToString().Length == 0 && row["out" + (j + 1).ToString()].ToString().Length > 0)
                                    {
                                        var nextClassId = dvTempshift[j]["attclassid"].ToString();
                                        var nextClass = dvAttClass.FirstOrDefault(p => p.AttClassID == nextClassId);
                                        if (nextClass != null && nextClass.NeedCheckin == false)  //如果下个时段为非必须签到，则跳过,东东
                                            continue;

                                        string checktime = row["out" + j.ToString()].ToString();
                                        string startchecktime = curdate + " " + dvTempshift[n + 1][att.business.bi.AttClassEnColumn.STARTCHECKIN];
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
                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = att.util.ShiftType.TEMPSHIFT + "(" + att.util.ShiftType.HOLIDAY + ")";
                            }
                            #endregion
                        }
                    }

                    if (!btemp && bHasHoliday)  //节假日并且不存在临时排班
                    {
                        //假期处理
                        System.Data.DataRow[] drs = dvHoliday.Table.Select("starttime <='" + curdate + "' and endtime>='" + curdate + "'");
                        if (drs.Length > 0)
                        {
                            foreach (var drv in drs)
                            {
                                var holidayType = drv[att.business.bi.HolidayEnColumn.HOLIDAYTYPE].ToString();
                                var holidayName = drv[att.business.bi.HolidayEnColumn.HOLIDAYNAME].ToString();
                                var deptids = drv[att.business.bi.HolidayEnColumn.APPLYDEPTIDS].ToString();

                                if (string.IsNullOrEmpty(deptids) == false)  //假期适用范围
                                {
                                    if (string.IsNullOrEmpty(employeebi.DEPTID) == true)
                                        continue;

                                    var result = deptids.Split(',').Contains(employeebi.DEPTID);
                                    if (result == false)
                                        continue;
                                }
                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = holidayType + "-" + holidayName;
                                bholiday = true;
                            }
                        }
                    }

                    if (hasshift && !btemp && !bholiday) //存在班次，且无临时排班和节假日
                    {
                        System.Data.DataRow[] drs = dvempshift.Table.Select("startdate<='" + curdate + "' and enddate>='" + curdate + "'");
                        if (drs.Length > 0)
                        {
                            string shifttype = drs[0][att.business.bi.EmployeeShiftEnColumn.SHIFTTYPE].ToString();
                            if (shifttype == att.util.ShiftType.SHIFT || shifttype == att.util.ShiftType.AUTOSHIFT)
                            {
                                #region 规律排班和智能排班
                                int dayspan = 0;
                                string employeeshiftid = drs[0]["employeeshiftid"].ToString();
                                if (drs[0]["cycleunit"].ToString() == att.util.ShiftCycleUnit.DAY)
                                {
                                    dayspan = (startdate - System.DateTime.Parse(drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE].ToString())).Days + i;
                                    dayspan = dayspan % int.Parse(drs[0]["cycle"].ToString());
                                }
                                else if (drs[0]["cycleunit"].ToString() == att.util.ShiftCycleUnit.WEEK)
                                {
                                    System.DateTime dt = System.DateTime.Parse(drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE].ToString());

                                    dayspan = (startdate - dt).Days + i + dt.DayOfWeek - System.DayOfWeek.Monday;
                                    int icycle = ((int)drs[0]["cycle"] * 7);
                                    dayspan = dayspan % icycle;
                                    if (dayspan == -1)
                                    {
                                        dayspan = icycle - 1;
                                    }
                                }
                                else if (drs[0]["cycleunit"].ToString() == att.util.ShiftCycleUnit.MONTH)
                                {
                                    System.DateTime dt = System.DateTime.Parse(drs[0][att.business.bi.EmployeeShiftEnColumn.STARTDATE].ToString());
                                    int monthspan = (startdate.Month - dt.Month) % int.Parse(drs[0]["cycle"].ToString());
                                    dayspan = startdate.Day + i + monthspan * 31 - 1;
                                }
                                if (shiftid != drs[0]["shiftid"].ToString())
                                {
                                    shiftid = drs[0]["shiftid"].ToString();
                                    dvDetail = detailbo.QueryByCondition("shiftid='" + drs[0]["shiftid"] + "' and attclassname is not null");
                                }
                                dvDetail.RowFilter = "startday=" + dayspan;
                                //dvDetail.Sort = att.business.bi.AttClassEnColumn.CHECKINTIME;
                                if (dvDetail.Count > 0)
                                {
                                    row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = drs[0]["shiftname"];
                                    //根据每天有多少次签入签出循环
                                    int nauto = 1;
                                    bool bhasnext = false; //用于智能排班的回溯功能
                                    for (int n = 1; n <= dvDetail.Count; n++)
                                    {
                                        bhasnext = false;
                                        if (curindex < dvAttRecord.Count)
                                        {
                                            string beginchecktime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKIN].ToString();
                                            DateTime _checktime = DateTime.Parse(
                                                string.Format("{0} {1}", dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(),
                                                                        dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));

                                            string endchecktime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKIN].ToString();
                                            string classintime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.CHECKINTIME].ToString();
                                            int lateignore = (int)dvDetail[n - 1][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                            int leaveignore = (int)dvDetail[n - 1][att.business.bi.AttClassEnColumn.LEAVEIGNORE];

                                            if (classintime.CompareTo(beginchecktime) < 0)
                                            {
                                                beginchecktime = startdate.AddDays(i - 1).ToString("yyyy-MM-dd") + " " + beginchecktime;
                                            }
                                            else
                                            {
                                                beginchecktime = curdate + " " + beginchecktime;
                                            }
                                            while (DateTime.Parse(beginchecktime) > _checktime)
                                            {
                                                //找签入记录
                                                curindex++;
                                                if (curindex == dvAttRecord.Count)
                                                {
                                                    break;
                                                }
                                                _checktime = DateTime.Parse(
                                                    string.Format("{0} {1}",
                                                        dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(),
                                                        dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));
                                            }
                                            if (curindex < dvAttRecord.Count)
                                            {
                                                if (endchecktime.CompareTo(classintime) < 0)
                                                {
                                                    endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                                }
                                                else
                                                {
                                                    endchecktime = curdate + " " + endchecktime;
                                                }
                                                if (endchecktime.CompareTo(beginchecktime) < 0)
                                                {
                                                    //跨天
                                                    endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                                }
                                                if (_checktime <= DateTime.Parse(endchecktime))
                                                {
                                                    //找到签入记录
                                                    if (shifttype == att.util.ShiftType.AUTOSHIFT)
                                                    {
                                                        row["in" + nauto] = _checktime.ToString((_checktime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                        if ((_checktime - System.DateTime.Parse(curdate + " " + classintime)).TotalMinutes > lateignore)
                                                        {
                                                            row["in" + nauto] += " ";
                                                        }

                                                        if (n < dvDetail.Count && dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString().CompareTo(dvDetail[n][att.business.bi.AttClassEnColumn.STARTCHECKIN]) >= 0)
                                                        {
                                                            bhasnext = true;
                                                        }

                                                    }
                                                    else
                                                    {
                                                        row["in" + n.ToString()] = _checktime.ToString((_checktime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                        if ((_checktime - System.DateTime.Parse(curdate + " " + classintime)).TotalMinutes > lateignore)
                                                        {
                                                            row["in" + n.ToString()] += " ";
                                                        }
                                                    }
                                                    curindex++;
                                                }
                                                else
                                                {
                                                    //智能排班中，如果签卡时间迟于最迟签入时间，那么就跳过这个时段
                                                    if (shifttype == att.util.ShiftType.AUTOSHIFT)
                                                    {
                                                        continue;
                                                    }
                                                }

                                                if (curindex < dvAttRecord.Count)
                                                {
                                                    beginchecktime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.STARTCHECKOUT].ToString();
                                                    endchecktime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.ENDCHECKOUT].ToString();
                                                    string classouttime = dvDetail[n - 1][att.business.bi.AttClassEnColumn.CHECKOUTTIME].ToString();

                                                    bool btwoday = (classouttime.CompareTo(dvDetail[n - 1][att.business.bi.AttClassEnColumn.CHECKINTIME]) <= 0);
                                                    if (btwoday)
                                                    {
                                                        if (beginchecktime.CompareTo(classouttime) <= 0)
                                                        {
                                                            beginchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + beginchecktime;
                                                        }
                                                        else
                                                        {
                                                            beginchecktime = curdate + " " + beginchecktime;
                                                        }
                                                        classouttime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + classouttime;
                                                    }
                                                    else
                                                    {
                                                        beginchecktime = curdate + " " + beginchecktime;
                                                        classouttime = curdate + " " + classouttime;
                                                    }
                                                    _checktime = DateTime.Parse(
                                                        string.Format("{0} {1}",
                                                            dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(),
                                                            dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));


                                                    if (DateTime.Parse(beginchecktime) > _checktime && shifttype == att.util.ShiftType.AUTOSHIFT)
                                                    {
                                                        //如果是智能排班就找下一个时段
                                                        if (bhasnext)
                                                        {
                                                            row["in" + nauto] = "";
                                                            curindex--;
                                                            continue;
                                                        }
                                                    }
                                                    while (DateTime.Parse(beginchecktime) > _checktime)
                                                    {
                                                        //找签出记录
                                                        curindex++;
                                                        if (curindex == dvAttRecord.Count)
                                                        {
                                                            break;
                                                        }
                                                        _checktime = DateTime.Parse(string.Format("{0} {1}",
                                                            dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(),
                                                            dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));
                                                    }
                                                    if (curindex < dvAttRecord.Count)
                                                    {
                                                        if (btwoday)
                                                        {
                                                            endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                                        }
                                                        else
                                                        {
                                                            if (classouttime.CompareTo(curdate + " " + endchecktime) > 0)
                                                            {
                                                                btwoday = true;
                                                                endchecktime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endchecktime;
                                                            }
                                                            else
                                                            {
                                                                endchecktime = curdate + " " + endchecktime;
                                                            }
                                                        }
                                                        if (_checktime <= DateTime.Parse(endchecktime))
                                                        {
                                                            //找到签出记录
                                                            if (shifttype == att.util.ShiftType.AUTOSHIFT)
                                                            {
                                                                if (row["in" + nauto].ToString().Length == 0)
                                                                {
                                                                    if (btwoday)
                                                                    {
                                                                        continue;
                                                                    }
                                                                    else
                                                                    {
                                                                        if (dvDetail.Count > n)
                                                                        {
                                                                            if (DateTime.Parse(beginchecktime) <= _checktime)
                                                                            {
                                                                                continue;
                                                                            }
                                                                        }
                                                                        row["out" + nauto] = _checktime.ToString((_checktime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                                        if ((System.DateTime.Parse(classouttime) - _checktime).TotalMinutes > leaveignore)
                                                                        {
                                                                            row["out" + nauto] += " ";
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    //找到签出记录
                                                                    row["out" + nauto] = _checktime.ToString((_checktime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                                    if ((System.DateTime.Parse(classouttime) - _checktime).TotalMinutes > leaveignore)
                                                                    {
                                                                        row["out" + nauto] += " ";
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                row["out" + n.ToString()] = _checktime.ToString((_checktime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                                if ((System.DateTime.Parse(classouttime) - _checktime).TotalMinutes > leaveignore)
                                                                {
                                                                    row["out" + n.ToString()] += " ";
                                                                }
                                                            }
                                                            curindex++;
                                                        }
                                                        else if (bhasnext)
                                                        {
                                                            curindex--;
                                                            row["in" + nauto] = "";
                                                            continue;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (shifttype == att.util.ShiftType.SHIFT)
                                        {
                                            row["attclass" + n.ToString()] = dvDetail[n - 1][att.business.bi.ShiftDetailEnColumn.ATTCLASSID];
                                            row["countovertime" + n.ToString()] = dvDetail[n - 1][att.business.bi.ShiftDetailEnColumn.COUNTOVERTIME];
                                        }
                                        else
                                        {
                                            if (row["in" + nauto].ToString().Length > 0)
                                            {
                                                row["attclass" + nauto.ToString()] = dvDetail[n - 1][att.business.bi.ShiftDetailEnColumn.ATTCLASSID];
                                                row["countovertime" + nauto.ToString()] = dvDetail[n - 1][att.business.bi.ShiftDetailEnColumn.COUNTOVERTIME];
                                                nauto++;
                                            }
                                        }
                                    }
                                    if (this.AttParams.AutoShiftHasAbsence && shifttype == att.util.ShiftType.AUTOSHIFT && row["in1"].ToString().Length == 0 && row["in2"].ToString().Length == 0
                                        && row["in3"].ToString().Length == 0 && row["in4"].ToString().Length == 0 && row["in5"].ToString().Length == 0)
                                    {
                                        row["attclass1"] = dvDetail[0][att.business.bi.ShiftDetailEnColumn.ATTCLASSID];
                                        row["countovertime1"] = dvDetail[0][att.business.bi.ShiftDetailEnColumn.COUNTOVERTIME];
                                    }

                                    if (shifttype == att.util.ShiftType.SHIFT)
                                    {
                                        //把缺少签入的签出记录移到下个时段的签入中使用
                                        for (int n = 0; n < dvDetail.Count - 1; n++)
                                        {
                                            int j = n + 1;
                                            if (row["in" + j.ToString()].ToString().Length == 0 && row["out" + j.ToString()].ToString().Length > 0 && row["in" + (j + 1).ToString()].ToString().Length == 0 && row["out" + (j + 1).ToString()].ToString().Length > 0)
                                            {
                                                var nextClassId = dvDetail[j]["attclassid"].ToString();
                                                var nextClass = dvAttClass.FirstOrDefault(p => p.AttClassID == nextClassId);
                                                if (nextClass != null && nextClass.NeedCheckin == false)  //如果下个时段为非必须签到，则跳过,东东
                                                    continue;

                                                string checktime = row["out" + j.ToString()].ToString();
                                                string startchecktime = curdate + " " + dvDetail[n + 1][att.business.bi.AttClassEnColumn.STARTCHECKIN];
                                                if (checktime.CompareTo(startchecktime) >= 0)
                                                {
                                                    row["in" + (j + 1).ToString()] = row["out" + j.ToString()];
                                                    row["out" + j.ToString()] = System.DBNull.Value;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = att.util.ShiftType.HOLIDAY;
                                }
                                #endregion
                            }
                            else if (shifttype == att.util.ShiftType.FREESHIFT)
                            {
                                #region 自由排班
                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = att.util.ShiftType.FREESHIFT;
                                if (curindex >= dvAttRecord.Count)
                                {
                                    row.EndEdit();
                                    continue;
                                }
                                while (curindex < dvAttRecord.Count && dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString().CompareTo(curdate) < 0)
                                {
                                    curindex++;
                                }
                                if (curindex < dvAttRecord.Count && dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString().CompareTo(curdate) > 0)
                                {
                                    row.EndEdit();
                                    continue;
                                }

                                for (int n = 1; n <= 5 && curindex < dvAttRecord.Count; n++)
                                {
                                    DateTime inTime = DateTime.Parse(string.Format("{0} {1}", dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(), dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));
                                    row["in" + n] = inTime.ToString((inTime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                    curindex++;
                                    if (curindex < dvAttRecord.Count)
                                    {
                                        var outTime = DateTime.Parse(string.Format("{0} {1}", dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString(), dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString()));

                                        if (curdate == outTime.ToString("yyyy-MM-dd") || (this.AttParams.IsMultiDay && startdate.AddDays(i + 1) >= DateTime.Parse(dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString())))
                                        {
                                            row["out" + n] = outTime.ToString((outTime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                            curindex++;
                                            if (curindex >= dvAttRecord.Count || dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString().CompareTo(curdate) > 0)
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            break;
                                        }
                                    }
                                }

                                #endregion
                            }
                            else if (shifttype == att.util.ShiftType.AUTOSHIFTLIST)
                            {
                                #region 智能班次组合
                                //根据签卡记录，智能比对班次，查找分值最高的班次
                                //分值计算方式：找到一次签卡记录，分数+10，未找到签卡，分值-1，获取最终分值最高的班次

                                row[att.business.bi.AttDailyReportEnColumn.SHIFTNAME] = drs[0]["shiftname"];
                                if (shiftid != drs[0]["shiftid"].ToString())
                                {
                                    shiftid = drs[0]["shiftid"].ToString();
                                    dvDetail = detailbo.QueryByCondition("shiftid='" + drs[0]["shiftid"] + "' and attclassname is not null");
                                }
                                if (curindex < dvAttRecord.Count)
                                {
                                    //当前签卡记录
                                    string checktime = dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[curindex][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                    //存储匹配的签卡记录序号
                                    ArrayList[] als = new ArrayList[2 * dvDetail.Count];
                                    //htList放入相同班组中的时段的序号数组
                                    System.Collections.Hashtable htList = new Hashtable();
                                    //储存匹配的签卡记录
                                    System.Collections.Hashtable htCheckTime = new Hashtable();
                                    int index = 0;
                                    //签入时间数组
                                    string[] checkintimes = new string[dvDetail.Count];
                                    //签出时间数组
                                    string[] checkouttimes = new string[dvDetail.Count];
                                    //根据班次对应的时段进行循环
                                    for (int n = 0; n < dvDetail.Count; n++)
                                    {
                                        string classintime = dvDetail[n][att.business.bi.AttClassEnColumn.CHECKINTIME].ToString();
                                        //int lateignore = (int)dvDetail[n][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                        string classouttime = dvDetail[n][att.business.bi.AttClassEnColumn.CHECKOUTTIME].ToString();
                                        //int leaveignore = (int)dvDetail[n][att.business.bi.AttClassEnColumn.LEAVEIGNORE];

                                        string begincheckintime = dvDetail[n][att.business.bi.AttClassEnColumn.STARTCHECKIN].ToString();
                                        string endcheckintime = dvDetail[n][att.business.bi.AttClassEnColumn.ENDCHECKIN].ToString();


                                        string begincheckouttime = dvDetail[n][att.business.bi.AttClassEnColumn.STARTCHECKOUT].ToString();
                                        string endcheckouttime = dvDetail[n][att.business.bi.AttClassEnColumn.ENDCHECKOUT].ToString();


                                        object o = dvDetail[n][att.business.bi.ShiftDetailEnColumn.STARTDAY];

                                        if (!htList.Contains(o))
                                        {
                                            ArrayList al = new ArrayList();
                                            al.Add(n);
                                            htList.Add(o, al);
                                        }
                                        else
                                        {
                                            ((ArrayList)htList[o]).Add(n);
                                        }
                                        als[2 * n] = new ArrayList();
                                        als[2 * n + 1] = new ArrayList();

                                        if (classintime.CompareTo(begincheckintime) < 0)
                                        {
                                            //跨天
                                            begincheckintime = startdate.AddDays(i - 1).ToString("yyyy-MM-dd") + " " + begincheckintime;
                                        }
                                        else
                                        {
                                            begincheckintime = curdate + " " + begincheckintime;
                                        }
                                        if (endcheckintime.CompareTo(classintime) < 0)
                                        {
                                            //跨天
                                            endcheckintime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endcheckintime;
                                        }
                                        else
                                        {
                                            endcheckintime = curdate + " " + endcheckintime;
                                        }
                                        if (classintime.CompareTo(begincheckouttime) > 0)
                                        {
                                            //跨天
                                            begincheckouttime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + begincheckouttime;
                                        }
                                        else
                                        {
                                            begincheckouttime = curdate + " " + begincheckouttime;
                                        }
                                        if (endcheckouttime.CompareTo(classintime) < 0)
                                        {
                                            //跨天
                                            endcheckouttime = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + endcheckouttime;
                                        }
                                        else
                                        {
                                            endcheckouttime = curdate + " " + endcheckouttime;
                                        }


                                        if (classouttime.CompareTo(classintime) < 0)
                                        {
                                            //跨天
                                            checkouttimes[n] = startdate.AddDays(i + 1).ToString("yyyy-MM-dd") + " " + classouttime;
                                        }
                                        else
                                        {
                                            checkouttimes[n] = curdate + " " + classouttime;
                                        }
                                        checkintimes[n] = curdate + " " + classintime;


                                        index = curindex;
                                        checktime = dvAttRecord[index][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[index][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                        while (checktime.CompareTo(endcheckouttime) <= 0)
                                        {
                                            if (checktime.CompareTo(begincheckintime) >= 0)
                                            {
                                                if (checktime.CompareTo(endcheckintime) <= 0)
                                                {
                                                    als[2 * n].Add(index);
                                                    htCheckTime[index] = checktime;
                                                }
                                            }
                                            if (checktime.CompareTo(begincheckouttime) >= 0)
                                            {
                                                if (checktime.CompareTo(endcheckouttime) <= 0)
                                                {
                                                    als[2 * n + 1].Add(index);
                                                    htCheckTime[index] = checktime;
                                                }
                                            }
                                            if (index == dvAttRecord.Count - 1)
                                            {
                                                break;
                                            }
                                            else
                                            {
                                                index++;
                                                checktime = dvAttRecord[index][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString() + " " + dvAttRecord[index][att.business.bi.AttRecordEnColumn.CHECKTIME].ToString();
                                            }
                                        }
                                    }
                                    int bestList = -1;
                                    int maxpoint = 0;  //最大分值
                                    int bestlastindex = 0;
                                    foreach (System.Collections.DictionaryEntry objDE in htList)
                                    {
                                        int satisfy = 0;
                                        int list = (int)objDE.Key;
                                        ArrayList al = (ArrayList)objDE.Value;
                                        index = curindex;
                                        foreach (object o in al)
                                        {
                                            int n = (int)o;
                                            bool bsatisfy = false;
                                            foreach (object o1 in als[2 * n])
                                            {
                                                if (((int)o1) >= index)
                                                {
                                                    //匹配一次，加10分
                                                    satisfy += 10;
                                                    index = ((int)o1) + 1;
                                                    bsatisfy = true;
                                                    break;
                                                }
                                            }
                                            if (!bsatisfy)
                                            {
                                                //不匹配一次，扣一分
                                                satisfy--;
                                            }
                                            bsatisfy = false;
                                            foreach (object o1 in als[2 * n + 1])
                                            {
                                                if (((int)o1) >= index)
                                                {
                                                    satisfy += 10;
                                                    index = ((int)o1) + 1;
                                                    bsatisfy = true;
                                                    break;
                                                }
                                            }
                                            if (!bsatisfy)
                                            {
                                                satisfy--;
                                            }
                                        }
                                        if (satisfy > maxpoint)
                                        {
                                            bestlastindex = index - 1;
                                            maxpoint = satisfy;
                                            bestList = (int)objDE.Key;
                                        }
                                    }

                                    if (bestList >= 0)
                                    {
                                        //如果符合的签卡记录多于两条，并且最后一次打卡在当天进行（如果签卡记录只找到一条，且该签卡记录属于跨天，则跳过）
                                        string lastbestcheckDate = dvAttRecord[bestlastindex][att.business.bi.AttRecordEnColumn.CHECKDATE].ToString();
                                        if (maxpoint > 10 || lastbestcheckDate.CompareTo(curdate) <= 0)
                                        {
                                            ArrayList alClass = (ArrayList)htList[bestList];
                                            int nclass = 0;
                                            index = curindex;
                                            foreach (object o in alClass)
                                            {
                                                nclass++;
                                                int n = (int)o;
                                                row["attclass" + nclass] = dvDetail[n][att.business.bi.AttClassEnColumn.ATTCLASSID];
                                                row["countovertime" + nclass] = dvDetail[n][att.business.bi.ShiftDetailEnColumn.COUNTOVERTIME];
                                                DateTime dtclassintime = System.DateTime.Parse(checkintimes[n]);
                                                int lateignore = (int)dvDetail[n][att.business.bi.AttClassEnColumn.LATEIGNORE];
                                                DateTime dtclassouttime = System.DateTime.Parse(checkouttimes[n]);
                                                int leaveignore = (int)dvDetail[n][att.business.bi.AttClassEnColumn.LEAVEIGNORE];

                                                foreach (object o1 in als[2 * n])
                                                {
                                                    if (((int)o1) >= index)
                                                    {
                                                        var inTime = DateTime.Parse(htCheckTime[o1].ToString());

                                                        row["in" + nclass] = inTime.ToString((inTime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
                                                        if (inTime > (dtclassintime.AddMinutes(lateignore)))
                                                        {
                                                            row["in" + nclass] += " ";
                                                        }
                                                        index = ((int)o1) + 1;
                                                        break;
                                                    }
                                                }
                                                foreach (object o1 in als[2 * n + 1])
                                                {
                                                    if (((int)o1) >= index)
                                                    {
                                                        var outTime = DateTime.Parse(htCheckTime[o1].ToString());


                                                        row["out" + nclass] = outTime.ToString((outTime.Second != 0) ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm"); //移除无效的秒
                                                        if (outTime < (dtclassouttime.AddMinutes(-leaveignore)))
                                                        {
                                                            row["out" + nclass] += " ";
                                                        }
                                                        index = ((int)o1) + 1;
                                                        break;
                                                    }
                                                }
                                                curindex = index;
                                            }
                                        }

                                    }
                                }
                                #endregion
                            }

                        }
                    }
                    #region 签卡记录，保存到ATTSTRING01列
                    if (dvResult.Table.Columns.Contains(att.business.bi.AttDailyReportEnColumn.ATTSTRING01) == true)
                    {
                        var _dvAttRecord = recordbo.QueryByCondition("employeeid='" + employeeid + "' and  checkdate ='" + curdate + "' order by checkdate,checktime");
                        if (_dvAttRecord != null && _dvAttRecord.Count > 0)
                        {
                            string times = "";
                            foreach (System.Data.DataRowView drv in _dvAttRecord)
                            {
                                times += drv[att.business.bi.AttRecordEnColumn.CHECKTIME] + ",";
                            }
                            row[att.business.bi.AttDailyReportEnColumn.ATTSTRING01] = times;
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
                        drv.Row.SetModified();//修改为编辑状态
                    }
                }
                //this.DeleteByCondition("employeeid='" + employeeid + "' and reportdate>='" + startdate.ToString("yyyy-MM-dd") + "' and reportdate<='" + enddate.ToString("yyyy-MM-dd") +"'");
                var reslt = this.UpdateRows(dvResult);

                humanresource.BusinessLogger.EndAction(employeeid + "日报表生成完毕，用时：" + (DateTime.Now - _log_startDate).ToString()); //记录日志
                return reslt;
            }
            catch (Exception ex)
            {
                this.ProcessException("生成日报表", ex.Message);
                humanresource.BusinessLogger.Error("生成日报表出错", ex);
                return false;
            }
        }
