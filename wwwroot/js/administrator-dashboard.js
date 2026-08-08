(function () {
    "use strict";

    const dashboard = document.querySelector("[data-administrator-dashboard]");
    if (!dashboard) return;

    const status = dashboard.querySelector("[data-administrator-status]");
    const search = dashboard.querySelector("[data-administrator-search]");
    const sidebarLinks = Array.from(dashboard.querySelectorAll(".administrator-sidebar [data-admin-nav]"));
    const sections = Array.from(dashboard.querySelectorAll("[data-admin-section]"));
    let noticeTimer;

    const showNotice = (message) => {
        if (!status) return;
        window.clearTimeout(noticeTimer);
        status.textContent = message;
        status.hidden = false;
        noticeTimer = window.setTimeout(() => {
            status.hidden = true;
        }, 3200);
    };

    const setActiveNavigation = (name) => {
        sidebarLinks.forEach((link) => {
            const isActive = link.dataset.adminNav === name;
            link.classList.toggle("is-active", isActive);
            if (isActive) link.setAttribute("aria-current", "page");
            else link.removeAttribute("aria-current");
        });
    };

    dashboard.querySelectorAll("[data-admin-nav]").forEach((link) => {
        link.addEventListener("click", () => {
            const name = link.dataset.adminNav;
            const target = document.getElementById(`admin-${name}`);
            if (!target) return;
            setActiveNavigation(name);
            target.scrollIntoView({ behavior: "smooth", block: "start" });
        });
    });

    const date = dashboard.querySelector("[data-administrator-date]");
    if (date) {
        date.dateTime = new Date().toISOString().slice(0, 10);
        date.textContent = new Intl.DateTimeFormat("en-NZ", {
            weekday: "long",
            day: "numeric",
            month: "long",
            year: "numeric"
        }).format(new Date());
    }

    dashboard.querySelector("[data-activity-button]")?.addEventListener("click", () => {
        document.getElementById("admin-activity")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });

    dashboard.querySelector("[data-administrator-logout]")?.addEventListener("click", () => {
        localStorage.removeItem("campusCoffeePortalSession");
    });

    if ("IntersectionObserver" in window) {
        const sectionObserver = new IntersectionObserver((entries) => {
            const visible = entries
                .filter((entry) => entry.isIntersecting)
                .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
            if (visible) setActiveNavigation(visible.target.dataset.adminSection);
        }, { rootMargin: "-20% 0px -62% 0px", threshold: [0, 0.1, 0.35] });
        sections.forEach((section) => sectionObserver.observe(section));

        const revealObserver = new IntersectionObserver((entries, observer) => {
            entries.forEach((entry) => {
                if (!entry.isIntersecting) return;
                entry.target.classList.add("is-visible");
                observer.unobserve(entry.target);
            });
        }, { rootMargin: "0px 0px -7% 0px", threshold: 0.08 });
        dashboard.querySelectorAll(".administrator-reveal").forEach((item) => revealObserver.observe(item));
    } else {
        dashboard.querySelectorAll(".administrator-reveal").forEach((item) => item.classList.add("is-visible"));
    }

    const animateCount = (element) => {
        const target = Number(element.dataset.adminCount);
        if (!Number.isFinite(target)) return;
        const duration = 950;
        const startedAt = performance.now();
        const frame = (now) => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = Math.round(target * eased).toLocaleString("en-NZ");
            if (progress < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    };
    dashboard.querySelectorAll("[data-admin-count]").forEach(animateCount);
    dashboard.querySelectorAll("[data-admin-decimal-count]").forEach((element) => {
        const target = Number(element.dataset.adminDecimalCount);
        if (!Number.isFinite(target)) return;
        const duration = 950;
        const startedAt = performance.now();
        const frame = (now) => {
            const progress = Math.min((now - startedAt) / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            element.textContent = (target * eased).toFixed(1);
            if (progress < 1) requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    });

    const filterItems = () => {
        const query = search.value.trim().toLocaleLowerCase();
        let matches = 0;
        dashboard.querySelectorAll("[data-admin-search-item]").forEach((item) => {
            const match = !query || item.textContent.toLocaleLowerCase().includes(query);
            item.classList.toggle("is-filtered-out", !match);
            if (match && query) matches += 1;
        });
        if (query) showNotice(`${matches} result${matches === 1 ? "" : "s"} found for “${search.value.trim()}”.`);
    };
    search?.addEventListener("input", filterItems);

    const userSearch = dashboard.querySelector("[data-user-search]");
    const userRoleFilter = dashboard.querySelector("[data-user-role-filter]");
    const filterUsers = () => {
        const query = userSearch?.value.trim().toLocaleLowerCase() || "";
        const role = userRoleFilter?.value || "all";
        dashboard.querySelectorAll("[data-user-row]").forEach((row) => {
            const matchesQuery = !query || row.textContent.toLocaleLowerCase().includes(query);
            const matchesRole = role === "all" || row.dataset.userRole === role;
            row.classList.toggle("is-user-filtered", !matchesQuery || !matchesRole);
        });
    };
    userSearch?.addEventListener("input", filterUsers);
    userRoleFilter?.addEventListener("change", filterUsers);

    document.addEventListener("keydown", (event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLocaleLowerCase() === "k") {
            event.preventDefault();
            search?.focus();
        }
    });

    dashboard.querySelectorAll(".administrator-segment button").forEach((button) => {
        button.addEventListener("click", () => {
            button.parentElement.querySelectorAll("button").forEach((item) => item.classList.remove("is-active"));
            button.classList.add("is-active");
            showNotice(`${button.textContent.trim()} chart selected. Static sample data is displayed.`);
        });
    });

    dashboard.querySelectorAll("[data-user-action]").forEach((button) => {
        button.addEventListener("click", () => {
            const action = button.dataset.userAction;
            const name = button.dataset.userName;
            if (action === "Approve") {
                const row = button.closest("tr");
                const badge = row?.querySelector(".administrator-badge");
                if (badge) {
                    badge.textContent = "Active";
                    badge.className = "administrator-badge administrator-badge--active";
                }
                button.textContent = "View";
                button.dataset.userAction = "View";
                showNotice(`${name} has been approved in this frontend demo.`);
                return;
            }
            showNotice(`${action} ${name}: account details are placeholder content.`);
        });
    });

    dashboard.querySelectorAll("[data-merchant-action]").forEach((button) => {
        button.addEventListener("click", () => {
            const card = button.closest("[data-merchant-application]");
            const name = card?.dataset.merchantName || "Merchant";
            const action = button.dataset.merchantAction;
            const badge = card?.querySelector("[data-merchant-status]");

            if (action === "view") {
                showNotice(`${name} application opened in preview mode.`);
                return;
            }

            if (badge) {
                const approved = action === "approve";
                badge.textContent = approved ? "Approved" : "Declined";
                badge.className = `administrator-badge administrator-badge--${approved ? "active" : "inactive"}`;
            }
            card?.querySelectorAll("[data-merchant-action]").forEach((actionButton) => {
                actionButton.disabled = true;
            });
            showNotice(`${name} has been ${action === "approve" ? "approved" : "declined"} in this frontend demo.`);
        });
    });

    dashboard.querySelectorAll("[data-product-action]").forEach((button) => {
        button.addEventListener("click", () => {
            const card = button.closest("[data-product-card]");
            const name = card?.dataset.productName || "Product";
            const action = button.dataset.productAction;
            const badge = card?.querySelector("[data-product-status]");
            const state = action === "approve"
                ? { label: "Approved", modifier: "active", message: "approved" }
                : action === "hide"
                    ? { label: "Hidden", modifier: "inactive", message: "hidden" }
                    : { label: "Removed", modifier: "inactive", message: "removed" };
            if (badge) {
                badge.textContent = state.label;
                badge.className = `administrator-badge administrator-badge--${state.modifier}`;
            }
            card?.classList.toggle("is-moderated", action === "delete");
            showNotice(`${name} has been ${state.message} in this frontend demo.`);
        });
    });

    dashboard.querySelectorAll("[data-order-archive]").forEach((button) => {
        button.addEventListener("click", () => {
            const row = button.closest("[data-order-row]");
            const orderId = row?.dataset.orderId || "Order";
            const badge = row?.querySelector("[data-order-status]");
            if (badge) {
                badge.textContent = "Archived";
                badge.className = "administrator-badge administrator-badge--inactive";
            }
            row?.classList.add("is-archived");
            button.disabled = true;
            button.textContent = "Archived";
            showNotice(`${orderId} has been archived in this frontend demo.`);
        });
    });

    dashboard.querySelectorAll("[data-feedback-action]").forEach((button) => {
        button.addEventListener("click", () => {
            const card = button.closest("[data-feedback-card]");
            const author = card?.dataset.feedbackAuthor || "Customer";
            const action = button.dataset.feedbackAction;
            const badge = card?.querySelector("[data-feedback-status]");
            const state = action === "keep"
                ? { label: "Visible", modifier: "active", message: "kept visible" }
                : action === "hide"
                    ? { label: "Hidden", modifier: "inactive", message: "hidden" }
                    : action === "delete"
                        ? { label: "Deleted", modifier: "inactive", message: "deleted" }
                        : { label: "Reviewed", modifier: "ready", message: "marked as reviewed" };
            if (badge) {
                badge.textContent = state.label;
                badge.className = `administrator-badge administrator-badge--${state.modifier}`;
            }
            showNotice(`${author}’s feedback has been ${state.message}.`);
        });
    });

    dashboard.querySelectorAll("[data-placeholder-action]").forEach((button) => {
        button.addEventListener("click", () => {
            showNotice(`${button.dataset.placeholderAction} is ready for future backend integration.`);
        });
    });

    dashboard.querySelector("[data-export-report]")?.addEventListener("click", () => {
        showNotice("Report prepared with static sample data. Export will be connected later.");
    });

    dashboard.querySelector("[data-admin-action='backup']")?.addEventListener("click", () => {
        showNotice("Database backup completed for this static frontend preview.");
    });

    dashboard.querySelectorAll("[data-setting-name]").forEach((input) => {
        input.addEventListener("change", () => {
            showNotice(`${input.dataset.settingName} ${input.checked ? "enabled" : "disabled"} for this preview.`);
        });
    });
}());
