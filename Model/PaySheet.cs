// Models/PaySheet.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TRIVORA_API.Models
{
    [Table("paySheet")]
    public class PaySheet
    {
        [Key]
        [Column("id_0")]
        public int Id { get; set; }

        [Column("empID_1")]
        public int EmployeeId { get; set; }

        [Column("month_2")]
        public string Month { get; set; } = string.Empty;

        [Column("basic_3")]
        public double? Basic { get; set; }

        [Column("br_4")]
        public double? Br { get; set; }

        [Column("totalBasic_5")]
        public double? TotalBasic { get; set; }

        [Column("salaryForEpf_6")]
        public double? SalaryForEpf { get; set; }

        [Column("workingDays_7")]
        public double? WorkingDays { get; set; }

        [Column("noPayDays_8")]
        public double? NoPayDays { get; set; }

        [Column("ot_NormalHours_9")]
        public double? OtNormalHours { get; set; }

        [Column("otSundaHours_10")]
        public double? OtSundayHours { get; set; }

        [Column("otNormal_11")]
        public double? OtNormal { get; set; }

        [Column("otSunday_12")]
        public double? OtSunday { get; set; }

        [Column("extrOtHours_13")]
        public double? ExtraOtHours { get; set; }

        [Column("extraOt_14")]
        public double? ExtraOt { get; set; }

        [Column("addDayCount_15")]
        public double? AddDayCount { get; set; }

        [Column("addDay_16")]
        public double? AddDay { get; set; }

        [Column("fixedAllowGradeAll_17")]
        public double? FixedAllowGradeAll { get; set; }

        [Column("fixedAllowPosition_18")]
        public double? FixedAllowPosition { get; set; }

        [Column("attendnceAllownce_19")]
        public double? AttendanceAllowance { get; set; }

        [Column("otherAllowance_20")]
        public double? OtherAllowance { get; set; }

        [Column("totaEarnings_21")]
        public double? TotalEarnings { get; set; }

        [Column("grossSalary_22")]
        public double? GrossSalary { get; set; }

        [Column("lateHours_23")]
        public double? LateHours { get; set; }

        [Column("late_24")]
        public double? Late { get; set; }

        [Column("noPay_25")]
        public double? NoPay { get; set; }

        [Column("advanced_26")]
        public double? Advanced { get; set; }

        [Column("otherDeduct_27")]
        public double? OtherDeduct { get; set; }

        [Column("Otherloan_28")]
        public double? OtherLoan { get; set; }

        [Column("epf8_29")]
        public double? Epf8 { get; set; }

        [Column("welfareMember_30")]
        public double? WelfareMember { get; set; }

        [Column("welfareDead_31")]
        public double? WelfareDead { get; set; }

        [Column("payeeStampDuty_32")]
        public double? PayeeStampDuty { get; set; }

        [Column("easyPay1_33")]
        public double? EasyPay1 { get; set; }

        [Column("easyPay2_34")]
        public double? EasyPay2 { get; set; }

        [Column("easyPay3_35")]
        public double? EasyPay3 { get; set; }

        [Column("welfareLoan_36")]
        public double? WelfareLoan { get; set; }

        [Column("donation_37")]
        public double? Donation { get; set; }

        [Column("totalDeduction_38")]
        public double? TotalDeduction { get; set; }

        [Column("netSalary_39")]
        public double? NetSalary { get; set; }

        [Column("coinsBF_40")]
        public double? CoinsBF { get; set; }

        [Column("totalPayble_41")]
        public double? TotalPayable { get; set; }

        [Column("pay_42")]
        public double? Pay { get; set; }

        [Column("epf12_43")]
        public double? Epf12 { get; set; }

        [Column("epf3_44")]
        public double? Epf3 { get; set; }

        [Column("coinCF_45")]
        public double? CoinCF { get; set; }

        [Column("payeeNew_46")]
        public double PayeeNew { get; set; }

        [Column("allow01_47")]
        public double Allow01 { get; set; }

        [Column("allow02_48")]
        public double Allow02 { get; set; }

        [Column("allow03_49")]
        public double Allow03 { get; set; }

        [Column("allow04_50")]
        public double Allow04 { get; set; }

        // Navigation property
        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }

    // DTOs for API
  // Models/PaySheetDto.cs
public class PaySheetDto
{
    public int Id { get; set; }
    public string? Designation { get; set; } 
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    
    // ========== EARNINGS ==========
    public double? Basic { get; set; }           // basic_3
    public double? Br { get; set; }              // br_4
    public double? TotalBasic { get; set; }      // totalBasic_5
    public double? SalaryForEpf { get; set; }    // salaryForEpf_6
    public double? WorkingDays { get; set; }     // workingDays_7
    public double? NoPayDays { get; set; }       // noPayDays_8
    
    // ========== OVERTIME ==========
    public double? OtNormalHours { get; set; }   // ot_NormalHours_9
    public double? OtSundayHours { get; set; }   // otSundaHours_10
    public double? OtNormal { get; set; }        // otNormal_11
    public double? OtSunday { get; set; }        // otSunday_12
    public double? ExtraOtHours { get; set; }    // extrOtHours_13
    public double? ExtraOt { get; set; }         // extraOt_14
    public double? AddDayCount { get; set; }     // addDayCount_15
    public double? AddDay { get; set; }          // addDay_16
    
    // ========== ALLOWANCES ==========
    public double? FixedAllowGradeAll { get; set; }    // fixedAllowGradeAll_17
    public double? FixedAllowPosition { get; set; }    // fixedAllowPosition_18
    public double? AttendanceAllowance { get; set; }   // attendnceAllownce_19
    public double? OtherAllowance { get; set; }        // otherAllowance_20
    public double? TotalEarnings { get; set; }         // totaEarnings_21
    public double? GrossSalary { get; set; }           // grossSalary_22
    
    // ========== DEDUCTIONS ==========
    public double? LateHours { get; set; }      // lateHours_23
    public double? Late { get; set; }           // late_24
    public double? NoPay { get; set; }          // noPay_25
    public double? Advanced { get; set; }       // advanced_26
    public double? OtherDeduct { get; set; }    // otherDeduct_27
    public double? OtherLoan { get; set; }      // Otherloan_28
    public double? Epf8 { get; set; }           // epf8_29
    public double? WelfareMember { get; set; }  // welfareMember_30
    public double? WelfareDead { get; set; }    // welfareDead_31
    public double? PayeeStampDuty { get; set; } // payeeStampDuty_32
    public double? EasyPay1 { get; set; }       // easyPay1_33
    public double? EasyPay2 { get; set; }       // easyPay2_34
    public double? EasyPay3 { get; set; }       // easyPay3_35
    public double? WelfareLoan { get; set; }    // welfareLoan_36
    public double? Donation { get; set; }       // donation_37
    public double? TotalDeduction { get; set; } // totalDeduction_38
    
    // ========== SUMMARY ==========
    public double? NetSalary { get; set; }      // netSalary_39
    public double? CoinsBF { get; set; }        // coinsBF_40
    public double? TotalPayable { get; set; }   // totalPayble_41
    public double? Pay { get; set; }            // pay_42
    public double? Epf12 { get; set; }          // epf12_43
    public double? Epf3 { get; set; }           // epf3_44
    public double? CoinCF { get; set; }         // coinCF_45
    public double? PayeeNew { get; set; }       // payeeNew_46
    public double? Allow01 { get; set; }        // allow01_47
    public double? Allow02 { get; set; }        // allow02_48
    public double? Allow03 { get; set; }        // allow03_49
    public double? Allow04 { get; set; }        // allow04_50
}
    public class PaySheetRequestDto
    {
       public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNo { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    
    // ========== EARNINGS ==========
    public double? Basic { get; set; }           // basic_3
    public double? Br { get; set; }              // br_4
    public double? TotalBasic { get; set; }      // totalBasic_5
    public double? SalaryForEpf { get; set; }    // salaryForEpf_6
    public double? WorkingDays { get; set; }     // workingDays_7
    public double? NoPayDays { get; set; }       // noPayDays_8
    
    // ========== OVERTIME ==========
    public double? OtNormalHours { get; set; }   // ot_NormalHours_9
    public double? OtSundayHours { get; set; }   // otSundaHours_10
    public double? OtNormal { get; set; }        // otNormal_11
    public double? OtSunday { get; set; }        // otSunday_12
    public double? ExtraOtHours { get; set; }    // extrOtHours_13
    public double? ExtraOt { get; set; }         // extraOt_14
    public double? AddDayCount { get; set; }     // addDayCount_15
    public double? AddDay { get; set; }          // addDay_16
    
    // ========== ALLOWANCES ==========
    public double? FixedAllowGradeAll { get; set; }    // fixedAllowGradeAll_17
    public double? FixedAllowPosition { get; set; }    // fixedAllowPosition_18
    public double? AttendanceAllowance { get; set; }   // attendnceAllownce_19
    public double? OtherAllowance { get; set; }        // otherAllowance_20
    public double? TotalEarnings { get; set; }         // totaEarnings_21
    public double? GrossSalary { get; set; }           // grossSalary_22
    
    // ========== DEDUCTIONS ==========
    public double? LateHours { get; set; }      // lateHours_23
    public double? Late { get; set; }           // late_24
    public double? NoPay { get; set; }          // noPay_25
    public double? Advanced { get; set; }       // advanced_26
    public double? OtherDeduct { get; set; }    // otherDeduct_27
    public double? OtherLoan { get; set; }      // Otherloan_28
    public double? Epf8 { get; set; }           // epf8_29
    public double? WelfareMember { get; set; }  // welfareMember_30
    public double? WelfareDead { get; set; }    // welfareDead_31
    public double? PayeeStampDuty { get; set; } // payeeStampDuty_32
    public double? EasyPay1 { get; set; }       // easyPay1_33
    public double? EasyPay2 { get; set; }       // easyPay2_34
    public double? EasyPay3 { get; set; }       // easyPay3_35
    public double? WelfareLoan { get; set; }    // welfareLoan_36
    public double? Donation { get; set; }       // donation_37
    public double? TotalDeduction { get; set; } // totalDeduction_38
    
    // ========== SUMMARY ==========
    public double? NetSalary { get; set; }      // netSalary_39
    public double? CoinsBF { get; set; }        // coinsBF_40
    public double? TotalPayable { get; set; }   // totalPayble_41
    public double? Pay { get; set; }            // pay_42
    public double? Epf12 { get; set; }          // epf12_43
    public double? Epf3 { get; set; }           // epf3_44
    public double? CoinCF { get; set; }         // coinCF_45
    public double? PayeeNew { get; set; }       // payeeNew_46
    public double? Allow01 { get; set; }        // allow01_47
    public double? Allow02 { get; set; }        // allow02_48
    public double? Allow03 { get; set; }        // allow03_49
    public double? Allow04 { get; set; }        // allow04_50
    }
}