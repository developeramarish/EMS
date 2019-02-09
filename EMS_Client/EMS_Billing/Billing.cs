﻿/**
*  \file Billing.cs
*  \project INFO2180 - EMS System Term Project
*  \author The Char Stars - Attila Katona
*  \date 2018-11-16
*  \brief Primary interaction with the Billing Library
*  
*  The functions in this file are used to setup the Billing Class in the Billing library. See class 
*  header comment for more information on the contents of this file
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMS_Library
{
   /** 
   * \class Billing
   *
   * \brief <b>Brief Description</b> - This class is meant to handle all functions that relate to billing information for the clinic
   * 
   * The Billing class has access to the <i>MasterFile</i> which holds all billing code information like <b>DateInitilized</b>, <b>BillingCode</b>
   * and <b>Rate</b> in canadian dollars. This class is also apart of the EMS_Library namespace, which is common throughout all of the the non-UI based libraries found in the EMS System.
   * This class will have functions that will allow entering and updating billable encounter data. The billing codes will indicate
   * which fees are applicable for the encounter. It will be able to check for any flags to recall appointments, the UI will be able
   * to send a message of some kind to the scheduling module that will allow the receptionist to book a patient for a recall. The billing class
   * will also handle generating a monthly billing file in a comma seperated CSV file as well as reconcile monthly billing by using a 
   * response file that is generated by the Ministry of Health. Finally, there will be an option available to generate a monthly summary of all billings. All exceptions will be caught
   * using a try catch and logged by the logging class, Logging.cs.
   * 
   * \author <i>The Char Stars - Attila Katona</i>
   */
    public class Billing
    {
        private Dictionary<string, BillingRecord> allBillingCodes = new Dictionary<string, BillingRecord>(); /**< The string representation of all billing codes in the database.*/
        private Dictionary<string, ApptBillRecord> appointmentBillingRecords = new Dictionary<string, ApptBillRecord>();  /**< The string representation of all appointment records in the database.*/
        public List<string[]> flaggedEncounters = new List<string[]>();
        /**
         * \brief <b>Brief Description</b> - <b><i>Constructor</i></b> - Called upon to begin the process of handling all billing information for patients
         * \details <b>Details</b>
         *
         * Constructor for the Billing class, it populates a dictionary with values from a table using the fileIO class.
         * 
         * \param f - <b>FileIO</b> - This is FileIO object used to open the required files to populate the Dictionary.
         *        
         * \return As this is a <i>constructor</i> for the Billing class, nothing is returned
         * 
         * <exception cref="IndexOutOfRangeException">Thrown if nothing the string array has no values</exception>
         * <exception cref="ArgumentException">Thrown trying to add a billing code / appointmentBillingRecord with the same ID to the dictionary. Try/Catch block.</exception>
         */
        public Billing()
        {
            Logging.Log("Billing", "Billing", "Initialize the Billing object");
            FileIO.UpdateBillingCodesFromFile();
            foreach (string [] s in FileIO.ConvertTableToDictionary(FileIO.GetDataTable(FileIO.TableNames.BillingCodes)).Values)
            {
                allBillingCodes.Add(s[0], new BillingRecord(s));
            }

            foreach (string[] s in FileIO.ConvertTableToDictionary(FileIO.GetDataTable(FileIO.TableNames.AppointmentBills)).Values)
            {
                appointmentBillingRecords.Add(s[0], new ApptBillRecord(s));
            }

        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This adds a billing code to a patients appointment
        * \details <b>Details</b>
        *
        * This will allow the user to add a billing code under an appointmentID which will include the PatientID, AppointmentID
        * and a billing code that will relate to how much the appointment will cost.
        * 
        * \param appointmentBillingID - <b>string</b> - This is the appointment billing ID.
        * \param appointmentID - <b>string</b> - This is the appointment ID.
        * \param patientID - <b>string</b> - This is the patient ID.
        * \param billingCode - <b>string</b> - This is the billing code.
        * 
        * \return none - <b>void</b> - this method returns nothing     
        * 
        * <exception cref="ArgumentException">Thrown trying to add a billing code / appointmentBillingRecord with the same ID to the dictionary. Try/Catch block.</exception>
        */
        public bool AddNewRecord(string appointmentID, string patientID, string billingCode)
        {
            try
            {
                if (appointmentID != null && patientID != null && billingCode != null && allBillingCodes.ContainsKey(billingCode.ToUpper()))
                {
                    string apptBillingID = FileIO.GenerateTableIDString(FileIO.TableNames.AppointmentBills);
                    string[] userInfo = { apptBillingID, appointmentID, patientID, billingCode.ToUpper() };
                    appointmentBillingRecords.Add(apptBillingID, new ApptBillRecord(userInfo));
                    SaveApptBillingRecords();

                    Logging.Log("Billing", "AddNewRecord", ("Adding " + billingCode + " to Appointment ID : " + appointmentID + " Patient ID : " + patientID + " For ApptBilling ID : " + apptBillingID));

                    return true;
                }
                else
                {
                    Logging.Log("Billing", "AddNewRecord", "FAILED ADDING NEW RECORD");
                    return false;
                }
            }
            catch (Exception e)
            {
                Logging.Log(e,"Billing", "AddNewRecord", "FAILED ADDING NEW RECORD - EXCEPTION HIT");
                return false;
            }
        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This updates billing codes under a patients appointment
        * \details <b>Details</b>
        *
        * This will allow the user to update billing code under an appointmentID which will include the PatientID, AppointmentID
        * and a billing code that will relate to how much the appointment will cost.
        * 
        * \param appointmentBillingID - <b>string</b> - This is the appointment billing ID.
        * \param appointmentID - <b>string</b> - This is the appointment ID.
        * \param patientID - <b>string</b> - This is the patient ID.
        * \param billingCode - <b>string</b> - This is the billing code.
        * 
        * \return - <b>bool</b> - true if successfull and false if not    
        * <exception cref="ArgumentException">Thrown trying to update a billing code / appointmentBillingRecord with an ID not in the dictionary. Try/Catch block, make no change on error.</exception>
        */
        public bool UpdateRecord (string appointmentBillingID, string appointmentID, string patientID, string billingCode)
        {
            try
            {
                Logging.Log("Billing", "UpdateRecord", ("UPDATING " + billingCode + " to Appointment ID : " + appointmentID + " Patient ID : " + patientID + " For ApptBilling ID : " + appointmentBillingID));
                string[] userInfo = { appointmentBillingID, appointmentID, patientID, billingCode };
                appointmentBillingRecords.Remove(appointmentBillingID);
                appointmentBillingRecords.Add(appointmentID, new ApptBillRecord(userInfo));
                SaveApptBillingRecords();
                return true;
            }
            catch(Exception e)
            {
                Logging.Log(e, "Billing", "UpdateRecord", "FAILED UPDATING RECORD -EXCEPTION HIT");
                return false;
            }
        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This adds a recall flag to an appointment
        * \details <b>Details</b>
        *
        * This will allow the user to add a recall flag to an appointment that will show in the appointment requiring the patient 
        * to return for another for another appointment. This will generally be used by the physician directly following an appointment
        * to ensure the patient will be seen again. This method will set the RecallFlag field from the Appointment.cs
        * 
        * \param obj - <b>Scheduling</b> - The obj passed in to use for the UpdateAppointmentInfo method.
        * \param appointmentID - <b>int</b> - This is the appointment ID.
        * \param recallFlag - <b>int</b> - This is the Recall Flag
        * 
        * 
        * \return none - <b>void</b> - this method returns nothing  
        * 
        * <exception cref="ArgumentException">Thrown trying to update the flag of a billing code / appointmentBillingRecord with an ID not in the dictionary. Try/Catch block, make no change on error.</exception>
        * * <exception cref="Exception">Thrown trying to use Scheduling class method to update the flag of a billing code. Try/Catch block, make no change on error.</exception>
        */
        public bool FlagAppointment(Scheduling obj, int appointmentID, int recallFlag)
        {

            try
            {
                Logging.Log("Billing", "AddNewRecord", ("Flagged appointment for reccall Appointment ID: " + appointmentID + " Recall Flag: " + recallFlag));
                return obj.UpdateAppointmentInfo(appointmentID, recallFlag);
            }
            catch(Exception e)
            {
                Logging.Log(e, "Billing", "FlagAppointment", "FAILED FLAGGING APPOINTMENT-EXCEPTION HIT");
                return false;
            }
        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This will generate a monthly billing file
        * \details <b>Details</b>
        *
        * This will generate a monthly billing by searching throuhg the applications data file. The file will be used by the
        * Ministry of Health to provide payment to the clinic. The method will read all appointments and patient information gathering
        * the appropriate billing code from the data file. The process will  be able to look up and apply a fee from the fee schedule file
        *  provided by the Ministry of Health against all billable encounters. The output file will be in CSV format.
        * 
        * \return none - <b>void</b> - this method returns nothing  
        * 
        */
        public bool GenerateMonthlyBillingFile (Scheduling schedule, Demographics demo, int year, int month)
        {            
            List<string> billingFileInfo = new List<string>();
            string tmp;
            Patient patient;
            try
            {
                foreach (Appointment a in schedule.GetAppointmentsByMonth(new DateTime(year, month, 1)))
                {
                    foreach (ApptBillRecord abr in appointmentBillingRecords.Values)
                    {
                        if (a.AppointmentID.ToString() == abr.AppointmentID)
                        {
                            patient = demo.GetPatientByID(Int32.Parse(abr.PatientID));
                            tmp = schedule.GetDateByAppointmentID(a.AppointmentID).ToString("yyyyMMdd");
                            tmp += patient.HCN;
                            tmp += patient.Sex;
                            tmp += abr.BillingCode;
                            tmp += (allBillingCodes[abr.BillingCode].Cost * 10000).ToString("00000000000");

                            billingFileInfo.Add(tmp);
                        }
                    }
                }

                if(FileIO.SaveToFile(string.Format("{0}{1}MonthlyBillingFile", year, month), billingFileInfo))
                {
                    Logging.Log("Billing", "GenerateMonthlyBillingFile", ("Generated Monthly Billing File for YEAR: " + year + " and Month: " + month));
                    return true;
                }
                else
                {
                    Logging.Log("Billing", "GenerateMonthlyBillingFile", "FAILED GENERATING MONTHLY BILLING FILE");
                    return false;
                }
                
            }
            catch (Exception e)
            {
                Logging.Log(e,"Billing", "GenerateMonthlyBillingFile", "FAILED GENERATING MONTHLY BILLING FILE-EXCEPTION HIT");
                return false;
            }
        }
        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This will generate a monthly billing summary
        * \details <b>Details</b>
        *
        * This will generate a monthly billing summary by using the reconcileMonthlyBillingFile method. It will display Total Encounters Billed,
        * Totl Billed procedures, Recieved Total, Recieved Percentage,, Average Billing and Number of Encounters to follow up.
        * 
        * \return - <b>List<string></b> -nothing
        * 
        */
        public List<string> GenerateMonthlyBillingSummary(string month)
        {
            string filePath = month + "govFile.txt";
            return ReconcileMonthlyBilling(filePath);
        }
        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This will analyze the response file sent from the Ministry of Health.
        * \details <b>Details</b>
        *
        * The Ministry of Health will provide a response file to any submitted MonthlyBillingFile. The file will be analyzed to look for 
        * codes sent back from the ministry. It will look for codes; <i>PAID</i>, <i>DECL(Declined)</i>, <i>FHCV(Failed Validation)</i>, <i>CMOH(Contact MoH)</i>.
        * It will update the monthly summary and will also flag the appointments for review if the code sent back is <b>FHCV</b> or <b>CMOH</b>.
        * 
        * \return - <b>List<string></string></b> - this method will return the summarized monthly bill summary     
        */
        public List<string> ReconcileMonthlyBilling (string monthFilePath = "govFile.txt")
        {
            string tmpYear = monthFilePath.Substring(0, 4);
            string tmpMonth = monthFilePath.Substring(4, 2);

            double totalBilled = 0;
            double totalRcvd = 0;
            int totalFlagged = 0;
            Dictionary<string, string[]> reconciledAppointment = FileIO.GetTableFromFile(monthFilePath, FileIO.FileInputFormat.GovernmentResponse);

            Dictionary<string, double> theSummary = new Dictionary<string, double>();
            List<string> theSummaryDisplayed = new List<string>();

            theSummary.Add("TotalEncounters", reconciledAppointment.Count);

            foreach (KeyValuePair<string, string[]> data in reconciledAppointment)
            {
                try
                {
                    totalBilled = totalBilled + (Convert.ToDouble(data.Value[4]));
                    if (data.Value[5] == "PAID")
                    {
                        totalRcvd = totalRcvd + (Convert.ToDouble(data.Value[4]));
                    }
                    else if (data.Value[5] == "FHCV" || data.Value[5] == "CMOH")
                    {
                        totalFlagged++;
                        flaggedEncounters.Add(data.Value);
                    }

                }
                catch (FormatException f) { Logging.Log(f, "Billing", "ReconcileMonthlyBilling", "FAILED CONVERTING STRING TO DOUBLE - EXCEPTION"); }
                catch (OverflowException o) { Logging.Log(o, "Billing", "ReconcileMonthlyBilling", "FAILED CONVERTING STRING TO DOUBLE - EXCEPTION"); }
                
            }
            totalBilled = totalBilled / 10000;
            totalRcvd = totalRcvd / 10000;

            theSummary.Add("TotalBilled", totalBilled);
            theSummaryDisplayed.Add("Total Billed : " + totalBilled.ToString());

            theSummary.Add("TotalReceived", totalRcvd);
            theSummaryDisplayed.Add("Total Received : " + totalRcvd.ToString());

            theSummary.Add("ReceivedPercentage", ((totalRcvd / totalBilled) * 100));
            theSummaryDisplayed.Add("Received Percentage : " + ((totalRcvd / totalBilled) * 100).ToString());

            theSummary.Add("AverageBilling", (totalRcvd / reconciledAppointment.Count));
            theSummaryDisplayed.Add("Average Billing : " + (totalRcvd / reconciledAppointment.Count).ToString());

            theSummary.Add("NumberOfFollowUps", totalFlagged);
            theSummaryDisplayed.Add("Number of Follow Ups : " + totalFlagged.ToString());

            Demographics demographics = new Demographics();
            foreach (string[] s in flaggedEncounters)
            {
                Patient p = demographics.GetPatientByHCN(s[1]);
                if (p != null) { theSummaryDisplayed.Add(string.Format("{0} - {1},{2} - {3}", s[0], p.LastName, p.FirstName, s[3])); }
            }

            Logging.Log("Billing", "ReconcileMonthlyBilling", ("Summary displayed for Month: " + tmpMonth + " Year: " + tmpYear));
            return theSummaryDisplayed;
        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This will save the appointment billing records to file
        * \details <b>Details</b>
        *
        * This method will look at the dictionary called appointmentBillingRecords and save every record to a file. It will use the FileIO
        * class from FileIO.cs to add the record to a table and then add that table to a file.
        *        
        * \return none - <b>void</b> - this method returns nothing 
        * 
        * <exception cref="ArgumentNullException">Thrown if appointmentBillingRecords is null. Must confirm not null as null values cannot have a .Values field.</exception>
        */
        public void SaveApptBillingRecords()
        {
            FileIO.SetDataTable(FileIO.GetDataTableStructure(FileIO.TableNames.AppointmentBills), FileIO.TableNames.AppointmentBills);
            foreach (ApptBillRecord a in appointmentBillingRecords.Values)
            {
                FileIO.AddRecordToDataTable(a.ToStringArray(), FileIO.TableNames.AppointmentBills);
            }
        }

        /**
        * \brief <b>Brief Description</b> - Billing<b> <i>class method</i></b> - This will check to ensure any code used in application
        * \details <b>Details</b>
        *
        * This method will take in a code as a string and it will validate that the code passed in is valid. By valid meaning that it is
        * either PAID, DECL, FHCV and CMOH. These codes relate to either bill rates or codes generated by the Ministry of Health as validation form the sent invoices.
        *      
        * \param checkCode - <b>string</b> - This string will hold the code that needs to be validated
        *        
        * \return <b>bool[]</b> - The resulting true or false depending if the code has been validated
        * 
        * <exception cref="ArgumentNullException">Thrown if checkCode is null. Must confirm not null to check value.</exception>
        */
        public bool IsCodeValid(string checkCode)
        {
            switch (checkCode)
            {
                case "PAID":
                case "DECL":
                case "FHCV":
                case "CMOH":
                    return true;
                default:
                    return false;  
            }
        }
    }
}