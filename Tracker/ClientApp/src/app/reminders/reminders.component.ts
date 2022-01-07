import {Component, Inject, OnInit} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {FormControl} from "@angular/forms";
import {CronOptions} from "ngx-cron-editor";

@Component({
  selector: 'app-reminders',
  templateUrl: './reminders.component.html'
})
export class RemindersComponent implements OnInit {
  public reminders: Reminder[] = [];

  public cronForm?: FormControl;
  public cronOptions: CronOptions = {
    defaultTime: "00:00:00",

    hideMinutesTab: false,
    hideHourlyTab: false,
    hideDailyTab: false,
    hideWeeklyTab: false,
    hideMonthlyTab: false,
    hideYearlyTab: false,
    hideAdvancedTab: true,
    hideSpecificWeekDayTab: false,
    hideSpecificMonthWeekTab : false,

    use24HourTime: true,
    hideSeconds: false,

    cronFlavor: "quartz" //standard or quartz
  };
  ngOnInit(): void {
    this.cronForm = new FormControl('0 0 1/1 * *');
  }
  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    http.get<Reminder[]>(baseUrl + 'reminder').subscribe(result => {
      this.reminders = result;
    }, error => console.error(error));
  }
}

interface Reminder {
  id: number;
  name: string;
  cronLocal: string;
  nextRun?: Date;
  startDate?: Date;
  endDate?: Date;
  reminderTypeId: number;
  reminderType: ReminderType;
}

interface ReminderType {
  id: number;
  name: string;
}
