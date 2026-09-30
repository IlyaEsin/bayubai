// Deployed once by bootstrap.sh: a budget's start date cannot move, so it stays out of the per-deploy templates.
param startDate string
param amount int = 40
param email string

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'bb-monthly'
  properties: {
    amount: amount
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: { startDate: startDate }
    notifications: {
      actual80: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 80, thresholdType: 'Actual', contactEmails: [ email ] }
      actual100: { enabled: true, operator: 'GreaterThanOrEqualTo', threshold: 100, thresholdType: 'Actual', contactEmails: [ email ] }
      forecast100: { enabled: true, operator: 'GreaterThan', threshold: 100, thresholdType: 'Forecasted', contactEmails: [ email ] }
    }
  }
}
