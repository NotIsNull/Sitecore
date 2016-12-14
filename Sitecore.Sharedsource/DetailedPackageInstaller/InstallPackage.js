(function(window, sharedSource, undefined) {
    var ip = sharedSource.InstallPackage = sharedSource.InstallPackage || {};

    ip.Progress = function(percentage) {
        if (scForm === undefined) {
            return;
        }
        var progressIndicator = scForm.browser.getControl("ProgressIndicator");
        if (progressIndicator === undefined || progressIndicator === null) {
            return;
        }
        progressIndicator.setStyle("width: " + percentage);
    };

    ip.SetStatus = function(elementId, active) {
        if (scForm === undefined) {
            return;
        }
        var statusElement = scForm.browser.getControl(elementId);
        if (statusElement === undefined || statusElement === null) {
            return;
        }

        if (active) {
            statusElement.addClassName("active");
        } else {
            statusElement.removeClassName("active");
        }
    };

    ip.UpdateCountMessage = function (message) {
        ip.UpdateMessage("ItemCountMessage", message, false);
    };

    ip.UpdateLogMessage = function(message) {
        ip.UpdateMessage("PackageInstallMessage", message, true);

        // scroll to the bottom
        var scrollContainer = scForm.browser.getControl("ScrollContainer");
        if (scrollContainer === undefined || scrollContainer === null) {
            return;
        }
        scrollContainer.scrollTop = scrollContainer.scrollHeight;
    };

    ip.UpdateMessage = function (controlId, message, append) {
        if (scForm === undefined) {
            return;
        }
        var messageElement = scForm.browser.getControl(controlId);
        if (messageElement === undefined || messageElement === null) {
            return;
        }
        var newContent = append ? (messageElement.innerHTML + message) : message;
        messageElement.innerHTML = newContent;
    };

    ip.SetTimer = function () {
        window.setTimeout(ip.CheckStatus, 500);
    };

    ip.CheckStatus = function () {
        if (scForm === undefined) {
            return;
        }
        var monitor = document.getElementById("MonitorLogEdit");
        if (monitor.value === "true") {
            scForm.invoke("installer:CheckLogProgress");
            ip.SetTimer();
        }
    };


})(window, window.SharedSource || (window.SharedSource = {}));
