function switchDashboard(viewId, button) {

    document
        .querySelectorAll(".dashboard-view")
        .forEach(function (view) {

            view.classList.remove("active");

        });


    const selectedView =
        document.getElementById(viewId);


    if (selectedView) {

        selectedView.classList.add("active");

    }


    document
        .querySelectorAll(".tab-btn")
        .forEach(function (tab) {

            tab.classList.remove("active");

        });


    if (button) {

        button.classList.add("active");

    }


    setTimeout(function () {

        if (window.compactTrendChart) {

            window.compactTrendChart.resize();

        }


        if (window.yearlyTrendChart) {

            window.yearlyTrendChart.resize();

        }

    }, 100);

}


function updateClock() {

    const now = new Date();

    const indiaTime =
        new Intl.DateTimeFormat(
            "en-IN",
            {
                timeZone: "Asia/Kolkata",
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
                hour12: false
            }
        ).format(now);


    const indiaDate =
        new Intl.DateTimeFormat(
            "en-IN",
            {
                timeZone: "Asia/Kolkata",
                day: "2-digit",
                month: "short",
                year: "numeric"
            }
        ).format(now);


    const indiaClockTime =
        document.getElementById("indiaClockTime");


    const indiaClockDate =
        document.getElementById("indiaClockDate");


    if (indiaClockTime) {

        indiaClockTime.textContent =
            indiaTime;

    }


    if (indiaClockDate) {

        indiaClockDate.textContent =
            indiaDate;

    }


    const chinaTime =
        new Intl.DateTimeFormat(
            "en-GB",
            {
                timeZone: "Asia/Shanghai",
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
                hour12: false
            }
        ).format(now);


    const chinaDate =
        new Intl.DateTimeFormat(
            "en-GB",
            {
                timeZone: "Asia/Shanghai",
                day: "2-digit",
                month: "short",
                year: "numeric"
            }
        ).format(now);


    const chinaClockTime =
        document.getElementById("chinaClockTime");


    const chinaClockDate =
        document.getElementById("chinaClockDate");


    if (chinaClockTime) {

        chinaClockTime.textContent =
            chinaTime;

    }


    if (chinaClockDate) {

        chinaClockDate.textContent =
            chinaDate;

    }

}


updateClock();

setInterval(updateClock, 1000);


function initializeCurrentMonthChart() {

    const canvas =
        document.getElementById(
            "compactTrendCanvas"
        );


    if (!canvas) {

        console.warn(
            "compactTrendCanvas was not found."
        );

        return;

    }


    if (typeof Chart === "undefined") {

        console.error(
            "Chart.js is not loaded."
        );

        return;

    }


    if (window.compactTrendChart) {

        window.compactTrendChart.destroy();

        window.compactTrendChart = null;

    }


    const ctx =
        canvas.getContext("2d");


    window.compactTrendChart =
        new Chart(ctx, {

            type: "bar",


            data: {

                labels: [

                    "Allocated Budget",

                    "Total Expenses",

                    "Balance Remaining"

                ],


                datasets: [

                    {
                        label: "Allocated Budget",

                        data: [

                            2000000,

                            null,

                            null

                        ],

                        backgroundColor:
                            "#3b82f6",

                        borderColor:
                            "#3b82f6",

                        borderWidth: 1,

                        borderRadius: 5,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    },


                    {
                        label: "Total Expenses",

                        data: [

                            null,

                            0,

                            null

                        ],

                        backgroundColor:
                            "#ef4444",

                        borderColor:
                            "#ef4444",

                        borderWidth: 1,

                        borderRadius: 5,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    },


                    {
                        label: "Balance Remaining",

                        data: [

                            null,

                            null,

                            2000000

                        ],

                        backgroundColor:
                            "#10b981",

                        borderColor:
                            "#10b981",

                        borderWidth: 1,

                        borderRadius: 5,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    }

                ]

            },


            options: {

                responsive: true,

                maintainAspectRatio: false,


                animation: {

                    duration: 800

                },


                plugins: {

                    legend: {

                        display: false

                    },


                    tooltip: {

                        enabled: true,

                        callbacks: {

                            label:
                                function (context) {

                                    const value =
                                        Number(
                                            context.raw || 0
                                        );


                                    return (
                                        context.dataset.label +
                                        ": ₹" +
                                        value.toLocaleString(
                                            "en-IN",
                                            {
                                                minimumFractionDigits: 2,
                                                maximumFractionDigits: 2
                                            }
                                        )
                                    );

                                }

                        }

                    }

                },


                scales: {

                    x: {

                        grid: {

                            display: false

                        },


                        ticks: {

                            color: "#64748b",

                            font: {

                                size: 10

                            }

                        }

                    },


                    y: {

                        beginAtZero: true,

                        suggestedMax: 2500000,


                        grid: {

                            color: "#e2e8f0"

                        },


                        ticks: {

                            color: "#64748b",

                            font: {

                                size: 9

                            },


                            callback:
                                function (value) {

                                    return (
                                        "₹" +
                                        (
                                            value / 100000
                                        ).toFixed(1) +
                                        "L"
                                    );

                                }

                        }

                    }

                }

            }

        });


    const monthDisplay =
        document.getElementById(
            "currentMonthDisplay"
        );


    if (monthDisplay) {

        monthDisplay.textContent =
            new Intl.DateTimeFormat(
                "en-US",
                {
                    month: "long",
                    year: "numeric"
                }
            ).format(new Date());

    }

}


function initializeYearlyChart() {

    const canvas =
        document.getElementById(
            "yearlyTrendCanvas"
        );


    if (!canvas) {

        console.warn(
            "yearlyTrendCanvas was not found."
        );

        return;

    }


    if (typeof Chart === "undefined") {

        console.error(
            "Chart.js is not loaded."
        );

        return;

    }


    if (window.yearlyTrendChart) {

        window.yearlyTrendChart.destroy();

        window.yearlyTrendChart = null;

    }


    const ctx =
        canvas.getContext("2d");


    window.yearlyTrendChart =
        new Chart(ctx, {

            type: "bar",


            data: {

                labels: [

                    "2024",

                    "2025",

                    "2026"

                ],


                datasets: [

                    {
                        label:
                            "Allocated Budget",

                        data: [

                            3500000,

                            2500000,

                            2000000

                        ],

                        backgroundColor:
                            "#3b82f6",

                        borderColor:
                            "#3b82f6",

                        borderWidth: 1,

                        borderRadius: 4,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    },


                    {
                        label:
                            "Budget Utilized",

                        data: [

                            3153355.70,

                            2046618.23,

                            1108057.45

                        ],

                        backgroundColor:
                            "#dc2626",

                        borderColor:
                            "#dc2626",

                        borderWidth: 1,

                        borderRadius: 4,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    },


                    {
                        label:
                            "Cost Saved",

                        data: [

                            222521.00,

                            785412.95,

                            293416.55

                        ],

                        backgroundColor:
                            "#65a30d",

                        borderColor:
                            "#65a30d",

                        borderWidth: 1,

                        borderRadius: 4,

                        barPercentage: 0.55,

                        categoryPercentage: 0.65
                    }

                ]

            },


            options: {

                responsive: true,

                maintainAspectRatio: false,


                animation: {

                    duration: 800

                },


                plugins: {

                    legend: {

                        display: true,

                        position: "top",


                        labels: {

                            font: {

                                size: 9

                            },

                            boxWidth: 12,

                            boxHeight: 10,

                            padding: 8

                        }

                    },


                    tooltip: {

                        enabled: true,


                        callbacks: {

                            label:
                                function (context) {

                                    const value =
                                        Number(
                                            context.raw || 0
                                        );


                                    return (
                                        context.dataset.label +
                                        ": ₹" +
                                        value.toLocaleString(
                                            "en-IN",
                                            {
                                                minimumFractionDigits: 2,
                                                maximumFractionDigits: 2
                                            }
                                        )
                                    );

                                }

                        }

                    }

                },


                scales: {

                    x: {

                        grid: {

                            display: false

                        },


                        ticks: {

                            color: "#64748b",

                            font: {

                                size: 10

                            }

                        }

                    },


                    y: {

                        beginAtZero: true,

                        suggestedMax: 3500000,


                        grid: {

                            color: "#e2e8f0"

                        },


                        ticks: {

                            color: "#64748b",

                            font: {

                                size: 9

                            },


                            callback:
                                function (value) {

                                    if (
                                        value >= 10000000
                                    ) {

                                        return (
                                            "₹" +
                                            (
                                                value /
                                                10000000
                                            ).toFixed(1) +
                                            "Cr"
                                        );

                                    }


                                    if (
                                        value >= 100000
                                    ) {

                                        return (
                                            "₹" +
                                            (
                                                value /
                                                100000
                                            ).toFixed(1) +
                                            "L"
                                        );

                                    }


                                    return (
                                        "₹" +
                                        Number(value)
                                            .toLocaleString(
                                                "en-IN"
                                            )
                                    );

                                }

                        }

                    }

                }

            }

        });

}


function initializeDashboard() {

    console.log(
        "Initializing Testing Spare Management Dashboard..."
    );


    if (typeof Chart === "undefined") {

        console.error(
            "Chart.js is NOT available."
        );

        return;

    }


    console.log(
        "Chart.js loaded successfully. Version:",
        Chart.version
    );


    initializeCurrentMonthChart();

    initializeYearlyChart();

}


document.addEventListener(
    "DOMContentLoaded",
    function () {

        initializeDashboard();

    }
);