(function(window, hi, undefined) {
    var ip = hi.InstallPackage = hi.InstallPackage || {};

    ip.Progress = function(percentage) {
        if (scForm === undefined) {
            return;
        }
        var progressIndicator = scForm.browser.getControl('ProgressIndicator');
        if (progressIndicator === undefined || progressIndicator === null) {
            return;
        }
        progressIndicator.setStyle('width: ' + percentage);
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
            statusElement.addClassName('active');
        } else {
            statusElement.removeClassName('active');
        }
    };

    ip.UpdateMessage = function (message) {
        if (scForm === undefined) {
            return;
        }
        var messageElement = scForm.browser.getControl('PackageInstallMessage');
        if (messageElement === undefined || messageElement === null) {
            return;
        }
        var newContent = (messageElement.innerHTML + message);
        messageElement.innerHTML = newContent;

        // scroll to the bottom
        var scrollContainer = scForm.browser.getControl('ScrollContainer');
        if (scrollContainer === undefined || scrollContainer === null) {
            return;
        }
        scrollContainer.scrollTop = scrollContainer.scrollHeight;
    };

})(window, window.HI || (window.HI = {}));