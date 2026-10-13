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
    public class PaySheetDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeNo { get; set; } = string.Empty;
        public string Month { get; set; } = string.Empty;
        public double? Basic { get; set; }
        public double? Br { get; set; }
        public double? TotalBasic { get; set; }
        public double? SalaryForEpf { get; set; }
        public double? WorkingDays { get; set; }
        public double? NoPayDays { get; set; }
        public double? OtNormalHours { get; set; }
        public double? OtSundayHours { get; set; }
        public double? OtNormal { get; set; }
        public double? OtSunday { get; set; }
        public double? ExtraOtHours { get; set; }
        public double? ExtraOt { get; set; }
        public double? AddDayCount { get; set; }
        public double? AddDay { get; set; }
        public double? FixedAllowGradeAll { get; set; }
        public double? FixedAllowPosition { get; set; }
        public double? AttendanceAllowance { get; set; }
        public double? OtherAllowance { get; set; }
        public double? TotalEarnings { get; set; }
        public double? GrossSalary { get; set; }
        public double? LateHours { get; set; }
        public double? Late { get; set; }
        public double? NoPay { get; set; }
        public double? Advanced { get; set; }
        public double? OtherDeduct { get; set; }
        public double? OtherLoan { get; set; }
        public double? Epf8 { get; set; }
        public double? WelfareMember { get; set; }
        public double? WelfareDead { get; set; }
        public double? PayeeStampDuty { get; set; }
        public double? EasyPay1 { get; set; }
        public double? EasyPay2 { get; set; }
        public double? EasyPay3 { get; set; }
        public double? WelfareLoan { get; set; }
        public double? Donation { get; set; }
        public double? TotalDeduction { get; set; }
        public double? NetSalary { get; set; }
        public double? CoinsBF { get; set; }
        public double? TotalPayable { get; set; }
        public double? Pay { get; set; }
        public double? Epf12 { get; set; }
        public double? Epf3 { get; set; }
        public double? CoinCF { get; set; }
        public double PayeeNew { get; set; }
        public double Allow01 { get; set; }
        public double Allow02 { get; set; }
        public double Allow03 { get; set; }
        public double Allow04 { get; set; }
    }

    public class PaySheetRequestDto
    {
        public int EmployeeId { get; set; }
        public string Month { get; set; } = string.Empty;
        public double? Basic { get; set; }
        public double? Br { get; set; }
        public double? TotalBasic { get; set; }
        public double? SalaryForEpf { get; set; }
        public double? WorkingDays { get; set; }
        public double? NoPayDays { get; set; }
        public double? OtNormalHours { get; set; }
        public double? OtSundayHours { get; set; }
        public double? OtNormal { get; set; }
        public double? OtSunday { get; set; }
        public double? ExtraOtHours { get; set; }
        public double? ExtraOt { get; set; }
        public double? AddDayCount { get; set; }
        public double? AddDay { get; set; }
        public double? FixedAllowGradeAll { get; set; }
        public double? FixedAllowPosition { get; set; }
        public double? AttendanceAllowance { get; set; }
        public double? OtherAllowance { get; set; }
        public double? TotalEarnings { get; set; }
        public double? GrossSalary { get; set; }
        public double? LateHours { get; set; }
        public double? Late { get; set; }
        public double? NoPay { get; set; }
        public double? Advanced { get; set; }
        public double? OtherDeduct { get; set; }
        public double? OtherLoan { get; set; }
        public double? Epf8 { get; set; }
        public double? WelfareMember { get; set; }
        public double? WelfareDead { get; set; }
        public double? PayeeStampDuty { get; set; }
        public double? EasyPay1 { get; set; }
        public double? EasyPay2 { get; set; }
        public double? EasyPay3 { get; set; }
        public double? WelfareLoan { get; set; }
        public double? Donation { get; set; }
        public double? TotalDeduction { get; set; }
        public double? NetSalary { get; set; }
        public double? CoinsBF { get; set; }
        public double? TotalPayable { get; set; }
        public double? Pay { get; set; }
        public double? Epf12 { get; set; }
        public double? Epf3 { get; set; }
        public double? CoinCF { get; set; }
        public double PayeeNew { get; set; }
        public double Allow01 { get; set; }
        public double Allow02 { get; set; }
        public double Allow03 { get; set; }
        public double Allow04 { get; set; }
    }
}